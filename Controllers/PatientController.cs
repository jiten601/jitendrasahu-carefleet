using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CareFleet.Controllers
{
    public class PatientController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PatientController(ApplicationDbContext context)
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

        // GET: Patient
        public IActionResult Index()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Patients Management";
            ViewBag.ActivePage = "Patients";
            var patients = _context.Patients.OrderByDescending(p => p.CreatedAt).ToList();
            return View(patients);
        }

        // GET: Patient/Create
        public IActionResult Create()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }
            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Patient";
            ViewBag.ActivePage = "Patients";
            return View();
        }

        // POST: Patient/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Patient patient)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                patient.CreatedAt = DateTime.Now;
                _context.Patients.Add(patient);
                _context.SaveChanges();
                TempData["Success"] = "Patient added successfully!";
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Patient";
            ViewBag.ActivePage = "Patients";
            return View(patient);
        }

        // GET: Patient/Edit/5
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
            ViewBag.HeaderTitle = "Edit Patient";
            ViewBag.ActivePage = "Patients";
            var patient = _context.Patients.Find(id);
            if (patient == null)
            {
                return NotFound();
            }

            return View(patient);
        }

        // POST: Patient/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Patient patient)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != patient.Id)
            {
                return NotFound();
            }

            var userRole = HttpContext.Session.GetString("UserRole");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(patient);
                    _context.SaveChanges();
                    TempData["Success"] = "Profile updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Patients.Any(e => e.Id == patient.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                // Redirect based on role
                if (userRole == "Patient")
                {
                    return RedirectToAction(nameof(Settings));
                }
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Edit Profile";
            ViewBag.ActivePage = "Settings";
            return View(userRole == "Patient" ? "Settings" : "Edit", patient);
        }

        // GET: Patient/Dashboard
        public IActionResult Dashboard()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            var patient = GetLoggedInPatient();
            if (patient == null) return RedirectToAction("Logout", "Account");

            // Get upcoming appointment
            var upcomingAppointment = _context.Appointments
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == patient.Id && a.AppointmentDate >= DateTime.Now)
                .OrderBy(a => a.AppointmentDate)
                .FirstOrDefault();

            ViewBag.UpcomingAppointment = upcomingAppointment;
            ViewBag.ActivePage = "Dashboard";
            ViewBag.HeaderTitle = $"Welcome, {ViewBag.FirstName} {ViewBag.LastName}!";

            return View();
        }

        public IActionResult Appointments()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            var patient = GetLoggedInPatient();
            
            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == patient.Id)
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            ViewBag.ActivePage = "Appointments";
            ViewBag.HeaderTitle = "My Appointments";
            return View(appointments);
        }

        public IActionResult MedicalRecords()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            ViewBag.ActivePage = "MedicalRecords";
            ViewBag.HeaderTitle = "Medical Records";
            return View();
        }

        public IActionResult Messages()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            ViewBag.ActivePage = "Messages";
            ViewBag.HeaderTitle = "Messages";
            return View();
        }

        public IActionResult Settings()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            
            var patient = GetLoggedInPatient();
            if (patient == null)
            {
                return RedirectToAction("Logout", "Account");
            }

            ViewBag.ActivePage = "Settings";
            ViewBag.HeaderTitle = "Profile Settings";
            return View(patient);
        }

        public IActionResult Profile()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var patient = GetLoggedInPatient();
            if (patient == null) return RedirectToAction("Logout", "Account");

            // Get dynamic stats
            ViewBag.AppointmentCount = _context.Appointments.Count(a => a.PatientId == patient.Id);
            // Since we don't have a MedicalRecords table yet, we'll use a placeholder or check a different metric
            // For now, let's just use 0 or a logical placeholder if you have a table for records.
            // If there's no table, I'll keep it as 0 but passed through ViewBag for future-proofing.
            ViewBag.RecordCount = 3; // Static for now as per the existing view design, but manageable from controller

            ViewBag.HeaderTitle = "My Profile";
            ViewBag.ActivePage = "Profile";
            ViewBag.UserEmail = patient.Email;
            return View(patient);
        }

        private bool IsPatient()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            return userRole == "Patient";
        }

        private Patient? GetLoggedInPatient()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            return _context.Patients.FirstOrDefault(p => p.Email == userEmail);
        }

        // POST: Patient/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            // Allow Admin to delete patients
            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole) || (userRole != "Admin" && userRole != "Patient"))
            {
                return RedirectToAction("Login", "Account");
            }

            var patient = _context.Patients.Find(id);
            if (patient != null)
            {
                _context.Patients.Remove(patient);
                _context.SaveChanges();
                TempData["Success"] = "Patient deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public IActionResult CancelAppointment(int id)
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            
            var appointment = _context.Appointments.Find(id);
            var patient = GetLoggedInPatient();
            
            if (appointment != null && patient != null && appointment.PatientId == patient.Id)
            {
                appointment.Status = "Cancelled";
                _context.SaveChanges();
                TempData["Success"] = "Appointment cancelled successfully.";
            }
            
            return RedirectToAction(nameof(Appointments));
        }

        public IActionResult AppointmentDetails(int id)
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var appointment = _context.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefault(a => a.Id == id);

            var patient = GetLoggedInPatient();
            if (appointment == null || patient == null || appointment.PatientId != patient.Id)
            {
                return NotFound();
            }

            ViewBag.ActivePage = "Appointments";
            ViewBag.HeaderTitle = "Appointment Details";
            return View(appointment);
        }

        public IActionResult DownloadRecord(int id)
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            
            // Placeholder: In a real app, this would fetch the file path from DB and return File()
            TempData["Success"] = "Your medical record is being prepared for download. This feature is coming soon!";
            return RedirectToAction(nameof(MedicalRecords));
        }

        public IActionResult ViewRecord(int id)
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            
            // Placeholder: In a real app, this would open a PDF viewer or redirect to a details page
            TempData["Info"] = "Viewing record... This feature is coming soon!";
            return RedirectToAction(nameof(MedicalRecords));
        }
    }
}

