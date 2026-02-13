using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareFleet.Controllers
{
    public class DoctorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DoctorController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Check authentication helper
        private bool IsAuthenticated()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            return !string.IsNullOrEmpty(userEmail);
        }

        // Helper to set user info
        private void SetUserInfo()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (!string.IsNullOrEmpty(userEmail))
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                if (user != null)
                {
                    ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
                    ViewBag.FirstName = user.FirstName;
                    ViewBag.LastName = user.LastName;
                }
            }
        }

        // GET: Doctor
        public IActionResult Index()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Doctors Management";
            ViewBag.ActivePage = "Doctors";
            var doctors = _context.Doctors.OrderByDescending(d => d.CreatedAt).ToList();
            return View(doctors);
        }

        // GET: Doctor/Create
        public IActionResult Create()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }
            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Doctor";
            ViewBag.ActivePage = "Doctors";
            return View();
        }

        // POST: Doctor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Doctor doctor)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                if (_context.Doctors.Any(d => d.Email == doctor.Email))
                {
                    SetUserInfo();
                    ViewBag.HeaderTitle = "Add New Doctor";
                    ViewBag.ActivePage = "Doctors";
                    ViewBag.Error = "A doctor with this email already exists.";
                    return View(doctor);
                }

                if (_context.Doctors.Any(d => d.LicenseNumber == doctor.LicenseNumber))
                {
                    SetUserInfo();
                    ViewBag.HeaderTitle = "Add New Doctor";
                    ViewBag.ActivePage = "Doctors";
                    ViewBag.Error = "A doctor with this license number already exists.";
                    return View(doctor);
                }

                doctor.CreatedAt = DateTime.Now;
                _context.Doctors.Add(doctor);
                _context.SaveChanges();
                TempData["Success"] = "Doctor added successfully!";
                if (HttpContext.Session.GetString("UserRole") == "Admin")
                {
                    return RedirectToAction("Doctors", "Admin");
                }
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Doctor";
            ViewBag.ActivePage = "Doctors";
            return View(doctor);
        }

        // GET: Doctor/Edit/5
        public IActionResult Edit(int? id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null)
            {
                return NotFound();
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Edit Doctor";
            ViewBag.ActivePage = "Doctors";
            var doctor = _context.Doctors.Find(id);
            if (doctor == null)
            {
                return NotFound();
            }

            return View(doctor);
        }

        // POST: Doctor/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Doctor doctor)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != doctor.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingDoctor = _context.Doctors.FirstOrDefault(d => d.Email == doctor.Email && d.Id != id);
                if (existingDoctor != null)
                {
                    ViewBag.Error = "A doctor with this email already exists.";
                    return View(doctor);
                }

                existingDoctor = _context.Doctors.FirstOrDefault(d => d.LicenseNumber == doctor.LicenseNumber && d.Id != id);
                if (existingDoctor != null)
                {
                    SetUserInfo();
                    ViewBag.HeaderTitle = "Edit Doctor";
                    ViewBag.ActivePage = "Doctors";
                    ViewBag.Error = "A doctor with this license number already exists.";
                    return View(doctor);
                }

                doctor.UpdatedAt = DateTime.Now;
                _context.Update(doctor);
                _context.SaveChanges();
                TempData["Success"] = "Profile updated successfully!";
                
                var role = HttpContext.Session.GetString("UserRole");
                if (role == "Doctor")
                {
                    return RedirectToAction(nameof(Settings));
                }
                if (role == "Admin")
                {
                    return RedirectToAction("Doctors", "Admin");
                }
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Edit Profile";
            ViewBag.ActivePage = "Settings";
            return View(HttpContext.Session.GetString("UserRole") == "Doctor" ? "Settings" : "Edit", doctor);
        }

        // POST: Doctor/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = _context.Doctors.Find(id);
            if (doctor != null)
            {
                _context.Doctors.Remove(doctor);
                _context.SaveChanges();
                TempData["Success"] = "Doctor deleted successfully!";
            }

            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                return RedirectToAction("Doctors", "Admin");
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Doctor/Dashboard
        public IActionResult Dashboard()
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            var doctor = GetLoggedInDoctor();
            if (doctor == null) return RedirectToAction("Logout", "Account");

            // Get upcoming appointments
            var doctorName = $"Dr. {doctor.FirstName} {doctor.LastName}";
            var upcomingAppointments = _context.Set<Appointment>()
                .Where(a => (a.DoctorName.Contains(doctor.FirstName) && a.DoctorName.Contains(doctor.LastName)) && a.AppointmentDate >= DateTime.Now)
                .OrderBy(a => a.AppointmentDate)
                .Take(5)
                .ToList();

            ViewBag.UpcomingAppointments = upcomingAppointments;
            
            // Statistics
            // Statistics - Filtered by Doctor
            // 1. Total Patients: Count distinct patients who have had appointments with this doctor
            var patientNames = _context.Set<Appointment>()
                .Where(a => a.DoctorName.Contains(doctor.FirstName) && a.DoctorName.Contains(doctor.LastName))
                .Select(a => a.PatientName)
                .Distinct()
                .ToList();
                
            ViewBag.TotalPatients = patientNames.Count;
            ViewBag.ActivePatients = patientNames.Count; // Assuming Active = Total for now

            // 2. New Patients This Month: Count patients created this month who are associated with this doctor
            // Note: Ideally, we'd check created date of first appointment, but using Patient.CreatedAt for simplicity
            // mixed with association check.
            ViewBag.NewPatientsThisMonth = _context.Patients
                .AsEnumerable()
                .Count(p => p.CreatedAt.Month == DateTime.Now.Month && 
                           p.CreatedAt.Year == DateTime.Now.Year && 
                           patientNames.Contains($"{p.FirstName} {p.LastName}"));

            ViewBag.HeaderTitle = $"Welcome, Dr. {doctor.FirstName} {doctor.LastName}";
            ViewBag.ActivePage = "Dashboard";

            return View();
        }

        public IActionResult Appointments(string searchString)
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            var doctor = GetLoggedInDoctor();
            
            var doctorName = $"Dr. {doctor.FirstName} {doctor.LastName}";
            var appointmentsQuery = _context.Set<Appointment>()
                .Where(a => a.DoctorName.Contains(doctor.FirstName) && a.DoctorName.Contains(doctor.LastName));

            if (!string.IsNullOrEmpty(searchString))
            {
                appointmentsQuery = appointmentsQuery.Where(a => a.PatientName.Contains(searchString));
            }

            var appointments = appointmentsQuery
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            ViewBag.CurrentFilter = searchString;
            ViewBag.HeaderTitle = "My Appointments";
            ViewBag.ActivePage = "Appointments";
            return View(appointments);
        }

        public IActionResult Patients()
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            var doctor = GetLoggedInDoctor();

            // Filter patients who have had appointments with this doctor
            // Filter patients who have had appointments with this doctor
            var doctorName = $"Dr. {doctor.FirstName} {doctor.LastName}";
            var patientNames = _context.Set<Appointment>()
                .Where(a => a.DoctorName.Contains(doctor.FirstName) && a.DoctorName.Contains(doctor.LastName))
                .Select(a => a.PatientName)
                .Distinct()
                .ToList();

            // Fetch all patients and filter in memory to allow flexible string matching
            // Note: In a real app with large data, we should add PatientId to Appointment
            var allPatients = _context.Patients.ToList();
            var patients = allPatients
                .Where(p => patientNames.Any(n => n.Contains(p.FirstName, StringComparison.OrdinalIgnoreCase) && 
                                                n.Contains(p.LastName, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(p => p.Id)
                .ToList();

            ViewBag.HeaderTitle = "My Patients";
            ViewBag.ActivePage = "Patients";
            return View(patients);
        }

        public IActionResult Messages()
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            ViewBag.HeaderTitle = "Messages";
            ViewBag.ActivePage = "Messages";
            return View();
        }

        public IActionResult MedicalRecords()
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            ViewBag.HeaderTitle = "Medical Records Access";
            ViewBag.ActivePage = "MedicalRecords";
            return View();
        }

        public IActionResult Settings()
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            
            var doctor = GetLoggedInDoctor();
            if (doctor == null) return RedirectToAction("Logout", "Account");

            ViewBag.HeaderTitle = "Profile Settings";
            ViewBag.ActivePage = "Settings";
            ViewBag.UserEmail = doctor.Email;
            return View(doctor);
        }

        public IActionResult Profile()
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            
            var doctor = GetLoggedInDoctor();
            if (doctor == null) return RedirectToAction("Logout", "Account");

            ViewBag.HeaderTitle = "My Profile";
            ViewBag.ActivePage = "Profile";
            ViewBag.UserEmail = doctor.Email;
            return View(doctor);
        }


        private bool IsDoctor()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            return userRole == "Doctor";
        }

        private Doctor? GetLoggedInDoctor()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            return _context.Doctors.FirstOrDefault(d => d.Email == userEmail);
        }

        // POST: Doctor/ToggleStatus/5
        [HttpPost]
        public IActionResult ToggleStatus(int id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = _context.Doctors.Find(id);
            if (doctor != null)
            {
                doctor.IsActive = !doctor.IsActive;
                doctor.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
            }

            if (HttpContext.Session.GetString("UserRole") == "Admin")
            {
                return RedirectToAction("Doctors", "Admin");
            }
            return RedirectToAction(nameof(Index));
        }
        // POST: Doctor/UpdateAppointmentStatus
        [HttpPost]
        public IActionResult UpdateAppointmentStatus(int appointmentId, string status)
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");

            var appointment = _context.Set<Appointment>()
                .FirstOrDefault(a => a.Id == appointmentId);

            var doctor = GetLoggedInDoctor();
            if (doctor == null) return RedirectToAction("Logout", "Account");

            var doctorName = $"Dr. {(doctor.FirstName)} {(doctor.LastName)}";
            
            // Be more flexible with name matching
            bool isAssignedDoctor = appointment != null && 
                                   (appointment.DoctorName == doctorName || 
                                    (appointment.DoctorName.Contains(doctor.FirstName) && appointment.DoctorName.Contains(doctor.LastName)));

            if (appointment != null && isAssignedDoctor)
            {
                appointment.Status = status;
                
                // Find patient by name for notification
                var patient = _context.Patients.FirstOrDefault(p => (p.FirstName + " " + p.LastName) == appointment.PatientName);
                
                if (patient != null)
                {
                    // Create notification for patient
                    var notification = new Notification
                    {
                        PatientId = patient.Id,
                        Message = $"Your appointment with {doctorName} on {appointment.AppointmentDate:MMM dd} has been updated to: {status}.",
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                }
                
                _context.SaveChanges();
                TempData["Success"] = $"Appointment status updated to {status}!";
            }
            else
            {
                TempData["Error"] = "Unable to update appointment status. You may not have permission for this record.";
            }

            return RedirectToAction(nameof(Appointments));
        }

        public IActionResult AppointmentDetails(int id)
        {
            if (!IsDoctor()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var doctor = GetLoggedInDoctor();
            if (doctor == null) return RedirectToAction("Logout", "Account");

            var appointment = _context.Set<Appointment>().FirstOrDefault(a => a.Id == id);
            
            if (appointment == null) return NotFound();

            var doctorName = $"Dr. {doctor.FirstName} {doctor.LastName}";
            bool isAssignedDoctor = appointment.DoctorName == doctorName || 
                                   (appointment.DoctorName.Contains(doctor.FirstName) && appointment.DoctorName.Contains(doctor.LastName));

            if (!isAssignedDoctor)
            {
                TempData["Error"] = "Access Denied.";
                return RedirectToAction(nameof(Appointments));
            }

            ViewBag.ActivePage = "Appointments";
            ViewBag.HeaderTitle = "Appointment Details";
            return View("~/Views/Patient/AppointmentDetails.cshtml", appointment);
        }
    }
}

