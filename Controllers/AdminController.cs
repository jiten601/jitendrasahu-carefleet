using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

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

        public IActionResult Users(string searchTerm, string roleFilter, int page = 1)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            int pageSize = 10;
            var query = _context.Users.AsQueryable();

            // Search Filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(u => u.FirstName.ToLower().Contains(term) || 
                                       u.LastName.ToLower().Contains(term) || 
                                       u.Email.ToLower().Contains(term));
            }

            // Role Filter
            if (!string.IsNullOrEmpty(roleFilter))
            {
                query = query.Where(u => u.Role == roleFilter);
            }

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            
            // Ensure page is within bounds
            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var users = query.OrderByDescending(u => u.Id)
                             .Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToList();

            ViewBag.HeaderTitle = "User Management";
            ViewBag.ActivePage = "Users";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalUsers = totalItems;
            ViewBag.TotalItems = totalItems;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.RoleFilter = roleFilter;

            return View(users);
        }

        public IActionResult Doctors(string searchTerm, string statusFilter, int page = 1)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            int pageSize = 10;
            var query = _context.Doctors.AsQueryable();

            // Search Filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(d => d.FirstName.ToLower().Contains(term) || 
                                       d.LastName.ToLower().Contains(term) || 
                                       d.Email.ToLower().Contains(term) ||
                                       d.Specialization.ToLower().Contains(term) ||
                                       d.LicenseNumber.ToLower().Contains(term));
            }

            // Status Filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                bool isActive = statusFilter == "Active";
                query = query.Where(d => d.IsActive == isActive);
            }

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var doctors = query.OrderByDescending(d => d.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToList();

            ViewBag.HeaderTitle = "System Doctors";
            ViewBag.ActivePage = "Doctors";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalDoctors = totalItems;
            ViewBag.TotalItems = totalItems; // Added for pagination
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;

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
        public IActionResult AddDoctor(Doctor doctor, string password)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("Password", "Password is required for registration.");
                }
                else if (_context.Users.Any(u => u.Email == doctor.Email) || 
                         _context.Doctors.Any(d => d.Email == doctor.Email) || 
                         _context.Patients.Any(p => p.Email == doctor.Email))
                {
                    ModelState.AddModelError("Email", "A user with this email already exists in our system.");
                }
                else if (_context.Doctors.Any(d => d.LicenseNumber == doctor.LicenseNumber))
                {
                    ModelState.AddModelError("LicenseNumber", "License number already exists.");
                }
                else
                {
                    // 1. Create Profile Record
                    doctor.CreatedAt = DateTime.Now;
                    _context.Doctors.Add(doctor);

                    // 2. Create Authentication Record
                    var user = new ApplicationUser
                    {
                        FirstName = doctor.FirstName,
                        LastName = doctor.LastName,
                        Email = doctor.Email,
                        PasswordHash = HashPassword(password),
                        Role = "Doctor",
                        IsEmailConfirmed = true, // Admin registrations are pre-verified
                        CreatedAt = DateTime.Now
                    };
                    _context.Users.Add(user);

                    _context.SaveChanges();

                    // Notify all admins about the new doctor
                    NotifyAllAdmins($"New doctor added: Dr. {doctor.FirstName} {doctor.LastName} ({doctor.Email}).");

                    TempData["Success"] = "Doctor registered successfully with login access!";
                    return RedirectToAction(nameof(Doctors));
                }
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Doctor";
            ViewBag.ActivePage = "Doctors";
            return View(doctor);
        }

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public IActionResult Patients(string searchTerm, int page = 1)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            int pageSize = 10;
            var query = _context.Patients.AsQueryable();

            // Search Filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(p => p.FirstName.ToLower().Contains(term) || 
                                       p.LastName.ToLower().Contains(term) || 
                                       p.Email.ToLower().Contains(term) ||
                                       p.Id.ToString().Contains(term));
            }

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var patients = query.OrderByDescending(p => p.CreatedAt)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

            ViewBag.HeaderTitle = "Patients Management";
            ViewBag.ActivePage = "Patients";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPatients = totalItems;
            ViewBag.TotalItems = totalItems; // Added for pagination
            ViewBag.SearchTerm = searchTerm;

            return View(patients);
        }

        public IActionResult Appointments(string searchTerm, string statusFilter, string dateFilter, int page = 1)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            int pageSize = 10;
            var query = _context.Appointments.AsQueryable();

            // Search Filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(a => a.PatientName.ToLower().Contains(term) || 
                                       a.DoctorName.ToLower().Contains(term) || 
                                       a.Reason.ToLower().Contains(term));
            }

            // Status Filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            // Date Filter
            if (dateFilter == "Today")
            {
                var today = DateTime.Today;
                query = query.Where(a => a.AppointmentDate.Date == today);
            }

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            var appointments = query.OrderByDescending(a => a.AppointmentDate)
                                    .Skip((page - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToList();

            ViewBag.HeaderTitle = "System Appointments";
            ViewBag.ActivePage = "Appointments";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.DateFilter = dateFilter ?? "All";
            ViewBag.TotalAppointments = totalItems;

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
                bool emailExists = user.Email != model.Email && 
                                   (_context.Users.Any(u => u.Email == model.Email) || 
                                    _context.Doctors.Any(d => d.Email == model.Email) || 
                                    _context.Patients.Any(p => p.Email == model.Email));

                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Email already exists in our system.");
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

                var email = user.Email;

                // Remove general dependent records
                var messages = _context.Messages.Where(m => m.SenderEmail == email || m.ReceiverEmail == email).ToList();
                _context.Messages.RemoveRange(messages);

                var notifications = _context.Notifications.Where(n => n.ReceiverEmail == email).ToList();
                _context.Notifications.RemoveRange(notifications);

                // Delete Role-associated records
                if (user.Role == "Doctor")
                {
                    var doctor = _context.Doctors.FirstOrDefault(d => d.Email == email);
                    if (doctor != null) 
                    {
                        var prescriptions = _context.Prescriptions.Where(p => p.DoctorId == doctor.Id).ToList();
                        _context.Prescriptions.RemoveRange(prescriptions);
                        
                        _context.Doctors.Remove(doctor);
                    }
                }
                else if (user.Role == "Patient")
                {
                    var patient = _context.Patients.FirstOrDefault(p => p.Email == email);
                    if (patient != null)
                    {
                        var invoices = _context.Invoices.Where(i => i.PatientId == patient.Id).ToList();
                        _context.Invoices.RemoveRange(invoices);
                        
                        var prescriptions = _context.Prescriptions.Where(p => p.PatientId == patient.Id).ToList();
                        _context.Prescriptions.RemoveRange(prescriptions);
                        
                        var medicalRecords = _context.MedicalRecords.Where(m => m.PatientId == patient.Id).ToList();
                        _context.MedicalRecords.RemoveRange(medicalRecords);
                        
                        _context.Patients.Remove(patient);
                    }
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
            
            // Core Counts
            var totalUsers    = _context.Users.Count();
            var totalDoctors  = _context.Doctors.Count();
            var totalPatients = _context.Patients.Count();
            var totalAppointments = _context.Set<Appointment>().Count();

            // Appointment status breakdown
            var confirmed  = _context.Set<Appointment>().Count(a => a.Status == "Confirmed");
            var pending    = _context.Set<Appointment>().Count(a => a.Status == "Pending");
            var cancelled  = _context.Set<Appointment>().Count(a => a.Status == "Cancelled");
            var completed  = _context.Set<Appointment>().Count(a => a.Status == "Completed");

            // Revenue
            var totalRevenue = _context.Set<Appointment>()
                .Where(a => a.Status == "Confirmed" || a.Status == "Completed")
                .Sum(a => (decimal?)a.Fee) ?? 0;

            // Monthly growth (Last 6 months)
            var last6Months  = Enumerable.Range(0, 6).Select(i => DateTime.Now.AddMonths(-i)).Reverse().ToList();
            var patientGrowth = last6Months.Select(m => _context.Patients.Count(p => p.CreatedAt.Month == m.Month && p.CreatedAt.Year == m.Year)).ToList();
            var doctorGrowth  = last6Months.Select(m => _context.Doctors.Count(d => d.CreatedAt.Month == m.Month && d.CreatedAt.Year == m.Year)).ToList();

            // Monthly appointment counts (last 6 months)
            var appointmentMonthly = last6Months.Select(m => _context.Set<Appointment>().Count(a => a.AppointmentDate.Month == m.Month && a.AppointmentDate.Year == m.Year)).ToList();

            // Recent appointments (last 5)
            var recentAppointments = _context.Set<Appointment>()
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToList();

            // Active doctors
            var activeDoctors = _context.Doctors.Count(d => d.IsActive);

            ViewBag.TotalUsers         = totalUsers;
            ViewBag.TotalDoctors       = totalDoctors;
            ViewBag.TotalPatients      = totalPatients;
            ViewBag.TotalAppointments  = totalAppointments;
            ViewBag.AppointmentsConfirmed  = confirmed;
            ViewBag.AppointmentsPending    = pending;
            ViewBag.AppointmentsCancelled  = cancelled;
            ViewBag.AppointmentsCompleted  = completed;
            ViewBag.TotalRevenue           = totalRevenue;
            ViewBag.ActiveDoctors          = activeDoctors;
            ViewBag.Months                 = last6Months.Select(m => m.ToString("MMM")).ToList();
            ViewBag.PatientGrowth          = patientGrowth;
            ViewBag.DoctorGrowth           = doctorGrowth;
            ViewBag.AppointmentMonthly     = appointmentMonthly;
            ViewBag.RecentAppointments     = recentAppointments;

            ViewBag.HeaderTitle = "Reports & Analytics";
            ViewBag.ActivePage  = "Reports";
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
