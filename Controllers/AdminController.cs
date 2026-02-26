using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareFleet.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Dashboard()
        {
            // Check if user is logged in
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole != "Admin")
            {
                return RedirectToAction("Login", "Account");
            }

            // Get user from database
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            var userName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrEmpty(userName))
            {
                userName = "Admin";
            }

            // Get dashboard statistics
            var totalUsers = _context.Users.Count();
            var activeDoctors = _context.Doctors.Count(d => d.IsActive);
            var totalPatients = _context.Patients.Count();
            var totalAppointments = _context.Set<Appointment>().Count();
            var pendingAppointments = _context.Set<Appointment>().Count(a => a.Status == "Pending");

            // Recent Activity
            var recentDoctors = _context.Doctors.OrderByDescending(d => d.CreatedAt).Take(5).ToList();
            var recentPatients = _context.Patients.OrderByDescending(p => p.CreatedAt).Take(5).ToList();
            var recentAppointments = _context.Set<Appointment>().OrderByDescending(a => a.CreatedAt).Take(5).ToList();

            ViewBag.UserName = userName;
            ViewBag.FirstName = user.FirstName;
            ViewBag.LastName = user.LastName;
            ViewBag.HeaderTitle = "Dashboard";
            ViewBag.ActivePage = "Dashboard";
            ViewBag.TotalUsers = totalUsers;
            ViewBag.ActiveDoctors = activeDoctors;
            ViewBag.TotalPatients = totalPatients;
            ViewBag.TotalAppointments = totalAppointments;
            ViewBag.PendingAppointments = pendingAppointments;
            
            ViewBag.RecentDoctors = recentDoctors;
            ViewBag.RecentPatients = recentPatients;
            ViewBag.RecentAppointments = recentAppointments;

            return View();
        }

        public IActionResult Users()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            var users = _context.Users.OrderByDescending(u => u.Id).ToList();
            ViewBag.HeaderTitle = "User Management";
            ViewBag.ActivePage = "Users";
            return View(users);
        }

        public IActionResult Doctors()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            var doctors = _context.Doctors.OrderByDescending(d => d.CreatedAt).ToList();
            ViewBag.HeaderTitle = "Doctors Management";
            ViewBag.ActivePage = "Doctors";
            return View(doctors);
        }

        public IActionResult AddDoctor()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Doctor";
            ViewBag.ActivePage = "Doctors";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddDoctor(Doctor doctor)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                if (_context.Doctors.Any(d => d.Email == doctor.Email))
                {
                    ModelState.AddModelError("Email", "Email already exists.");
                }
                else
                {
                    doctor.CreatedAt = DateTime.Now;
                    _context.Doctors.Add(doctor);
                    _context.SaveChanges();

                    // Notify all admins about the new doctor
                    NotifyAllAdmins($"New doctor added: Dr. {doctor.FirstName} {doctor.LastName} ({doctor.Email}).");

                    TempData["Success"] = "Doctor added successfully!";
                    return RedirectToAction(nameof(Doctors));
                }
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Doctor";
            ViewBag.ActivePage = "Doctors";
            return View(doctor);
        }

        public IActionResult Patients()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            var patients = _context.Patients.OrderByDescending(p => p.CreatedAt).ToList();
            ViewBag.HeaderTitle = "Patients Management";
            ViewBag.ActivePage = "Patients";
            return View(patients);
        }

        public IActionResult Appointments()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            var appointments = _context.Set<Appointment>()
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            ViewBag.HeaderTitle = "Appointments Management";
            ViewBag.ActivePage = "Appointments";
            ViewBag.TotalAppointments = appointments.Count;
            return View(appointments);
        }

        public IActionResult AppointmentDetails(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var appointment = _context.Set<Appointment>().Find(id);
            if (appointment == null) return NotFound();

            ViewBag.HeaderTitle = "Appointment Details";
            ViewBag.ActivePage = "Appointments";
            return View("~/Views/Patient/AppointmentDetails.cshtml", appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelAppointment(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var appointment = _context.Set<Appointment>().Find(id);
            if (appointment != null)
            {
                appointment.Status = "Cancelled";
                _context.SaveChanges();
                TempData["Success"] = "Appointment cancelled successfully.";
            }

            return RedirectToAction(nameof(Appointments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmAppointment(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var appointment = _context.Set<Appointment>().Find(id);
            if (appointment != null)
            {
                appointment.Status = "Confirmed";
                _context.SaveChanges();
                TempData["Success"] = "Appointment confirmed successfully.";
            }

            return RedirectToAction(nameof(Appointments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAppointment(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var appointment = _context.Set<Appointment>().Find(id);
            if (appointment != null)
            {
                _context.Set<Appointment>().Remove(appointment);
                _context.SaveChanges();
                TempData["Success"] = "Appointment deleted successfully!";
            }

            return RedirectToAction(nameof(Appointments));
        }

        public IActionResult Settings()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            SetUserInfo();
            ViewBag.HeaderTitle = "System Settings";
            ViewBag.ActivePage = "Settings";
            ViewBag.UserEmail = userEmail;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(ApplicationUser model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(model.Id);
            if (user != null)
            {
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                // We don't allow email/role changes for security via this form

                _context.Users.Update(user);
                _context.SaveChanges();

                // Update session
                if (HttpContext.Session.GetString("UserEmail") == user.Email)
                {
                    HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                }

                TempData["Success"] = "Profile updated successfully!";
            }

            return RedirectToAction(nameof(Settings));
        }

        public IActionResult EditUser(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            ViewBag.HeaderTitle = "Edit User";
            ViewBag.ActivePage = "Users";
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditUser(ApplicationUser model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(model.Id);
            if (user == null) return NotFound();

            if (ModelState.IsValid)
            {
                // Check if email changed and if new email already exists
                if (user.Email != model.Email && _context.Users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email already exists.");
                }
                else
                {
                    // Update associated records if email or name changes
                    if (user.Role == "Doctor")
                    {
                        var doctor = _context.Doctors.FirstOrDefault(d => d.Email == user.Email);
                        if (doctor != null)
                        {
                            doctor.FirstName = model.FirstName;
                            doctor.LastName = model.LastName;
                            doctor.Email = model.Email;
                            _context.Doctors.Update(doctor);
                        }
                    }
                    else if (user.Role == "Patient")
                    {
                        var patient = _context.Patients.FirstOrDefault(p => p.Email == user.Email);
                        if (patient != null)
                        {
                            patient.FirstName = model.FirstName;
                            patient.LastName = model.LastName;
                            patient.Email = model.Email;
                            _context.Patients.Update(patient);
                        }
                    }

                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    user.Email = model.Email;
                    user.Role = model.Role;

                    _context.Users.Update(user);
                    _context.SaveChanges();

                    TempData["Success"] = "User updated successfully!";
                    return RedirectToAction(nameof(Users));
                }
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Edit User";
            ViewBag.ActivePage = "Users";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteUser(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(id);
            if (user != null)
            {
                // Check if user is deleting themselves
                if (user.Email == HttpContext.Session.GetString("UserEmail"))
                {
                    TempData["Error"] = "You cannot delete your own account!";
                    return RedirectToAction(nameof(Users));
                }

                // Delete associated records
                if (user.Role == "Doctor")
                {
                    var doctor = _context.Doctors.FirstOrDefault(d => d.Email == user.Email);
                    if (doctor != null) _context.Doctors.Remove(doctor);
                }
                else if (user.Role == "Patient")
                {
                    var patient = _context.Patients.FirstOrDefault(p => p.Email == user.Email);
                    if (patient != null) _context.Patients.Remove(patient);
                }

                _context.Users.Remove(user);
                _context.SaveChanges();
                TempData["Success"] = "User deleted successfully!";
            }

            return RedirectToAction(nameof(Users));
        }

        public IActionResult Reports()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            
            // Appointment status breakdown
            ViewBag.AppointmentsConfirmed = _context.Set<Appointment>().Count(a => a.Status == "Confirmed");
            ViewBag.AppointmentsPending = _context.Set<Appointment>().Count(a => a.Status == "Pending");
            ViewBag.AppointmentsCancelled = _context.Set<Appointment>().Count(a => a.Status == "Cancelled");
            
            // Monthly growth (Last 6 months)
            var last6Months = Enumerable.Range(0, 6).Select(i => DateTime.Now.AddMonths(-i)).Reverse().ToList();
            var patientGrowth = last6Months.Select(m => _context.Patients.Count(p => p.CreatedAt.Month == m.Month && p.CreatedAt.Year == m.Year)).ToList();
            var doctorGrowth = last6Months.Select(m => _context.Doctors.Count(d => d.CreatedAt.Month == m.Month && d.CreatedAt.Year == m.Year)).ToList();
            
            ViewBag.Months = last6Months.Select(m => m.ToString("MMM")).ToList();
            ViewBag.PatientGrowth = patientGrowth;
            ViewBag.DoctorGrowth = doctorGrowth;

            ViewBag.HeaderTitle = "Reports & Analytics";
            ViewBag.ActivePage = "Reports";
            return View();
        }

        private bool IsAdmin()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            return userRole == "Admin";
        }

        private void SetUserInfo()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user != null)
            {
                ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
                ViewBag.FirstName = user.FirstName;
                ViewBag.LastName = user.LastName;
                ViewBag.UnreadNotifications = _context.Notifications.Count(n => n.ReceiverEmail == userEmail && !n.IsRead);
            }
        }

        /// <summary>Sends a notification to every admin user in the system.</summary>
        private void NotifyAllAdmins(string message)
        {
            var adminEmails = _context.Users
                .Where(u => u.Role == "Admin")
                .Select(u => u.Email)
                .ToList();

            foreach (var email in adminEmails)
            {
                _context.Notifications.Add(new Notification
                {
                    ReceiverEmail = email,
                    Message = message,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }
            _context.SaveChanges();
        }
    }
}
