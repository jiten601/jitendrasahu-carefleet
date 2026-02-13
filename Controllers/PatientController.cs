using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CareFleet.Controllers
{
    public class PatientController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public PatientController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
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

                    // Fetch unread notifications
                    var patient = _context.Patients.FirstOrDefault(p => p.Email == userEmail);
                    if (patient != null)
                    {
                        ViewBag.UnreadNotifications = _context.Notifications.Count(n => n.PatientId == patient.Id && !n.IsRead);
                    }
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
            string viewName = userRole == "Patient" ? "~/Views/Patient/Settings.cshtml" : "~/Views/Patient/Edit.cshtml";
            return View(viewName, patient);
        }

        // GET: Patient/Dashboard
        public IActionResult Dashboard()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");

            SetUserInfo();
            var patient = GetLoggedInPatient();
            if (patient == null) return RedirectToAction("Logout", "Account");

            // Get upcoming appointment
            var firstName = (string)ViewBag.FirstName;
            var lastName = (string)ViewBag.LastName;
            var fullName = $"{firstName} {lastName}".Trim();

            var upcomingAppointment = _context.Set<Appointment>()
                .AsEnumerable()
                .Where(a => a.PatientName != null && 
                           (a.PatientName.Trim().Equals(fullName, StringComparison.OrdinalIgnoreCase) || 
                            (a.PatientName.Contains(firstName, StringComparison.OrdinalIgnoreCase) && 
                             a.PatientName.Contains(lastName, StringComparison.OrdinalIgnoreCase))) && 
                            a.AppointmentDate >= DateTime.Now)
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
            
            var firstName = (string)ViewBag.FirstName;
            var lastName = (string)ViewBag.LastName;
            var fullName = $"{firstName} {lastName}".Trim();

            var appointments = _context.Set<Appointment>()
                .AsEnumerable()
                .Where(a => a.PatientName != null && 
                           (a.PatientName.Trim().Equals(fullName, StringComparison.OrdinalIgnoreCase) || 
                            (a.PatientName.Contains(firstName, StringComparison.OrdinalIgnoreCase) && 
                             a.PatientName.Contains(lastName, StringComparison.OrdinalIgnoreCase))))
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
            
            var patient = GetLoggedInPatient();
            if (patient == null) return RedirectToAction("Logout", "Account");

            var records = _context.MedicalRecords
                .Where(r => r.PatientId == patient.Id)
                .OrderByDescending(r => r.DateIssued)
                .ToList();

            ViewBag.ActivePage = "MedicalRecords";
            ViewBag.HeaderTitle = "Medical Records";
            return View(records);
        }

        public IActionResult Messages(string? receiverEmail)
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();
            var patient = GetLoggedInPatient();
            if (patient == null) return RedirectToAction("Logout", "Account");

            // Get unique doctors from appointments
            var doctorNamesByApps = _context.Appointments
                .Where(a => a.PatientName.Contains(patient.FirstName) && a.PatientName.Contains(patient.LastName))
                .Select(a => a.DoctorName)
                .Distinct()
                .ToList();

            // Find matching doctor entities
            var doctorsFromApps = _context.Doctors
                .AsEnumerable()
                .Where(d => doctorNamesByApps.Any(n => n.Contains(d.FirstName) && n.Contains(d.LastName)))
                .ToList();

            // Get unique emails from existing messages
            var emailsFromMessages = _context.Messages
                .Where(m => m.SenderEmail == patient.Email || m.ReceiverEmail == patient.Email)
                .Select(m => m.SenderEmail == patient.Email ? m.ReceiverEmail : m.SenderEmail)
                .Distinct()
                .ToList();

            var messageContacts = _context.Doctors
                .Where(d => emailsFromMessages.Contains(d.Email))
                .ToList();

            // Union contacts
            var allContacts = doctorsFromApps.Union(messageContacts, new DoctorComparer()).ToList();

            ViewBag.Contacts = allContacts;
            ViewBag.SelectedContactEmail = receiverEmail ?? allContacts.FirstOrDefault()?.Email;
            string selectedEmail = ViewBag.SelectedContactEmail;

            if (!string.IsNullOrEmpty(selectedEmail))
            {
                var selectedDoctor = allContacts.FirstOrDefault(d => d.Email == selectedEmail);
                ViewBag.SelectedContactName = selectedDoctor != null ? $"Dr. {selectedDoctor.FirstName} {selectedDoctor.LastName}" : "Healthcare Provider";

                var chatHistory = _context.Messages
                    .Where(m => (m.SenderEmail == patient.Email && m.ReceiverEmail == selectedEmail) ||
                                (m.SenderEmail == selectedEmail && m.ReceiverEmail == patient.Email))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                ViewBag.ChatHistory = chatHistory;

                // Mark received messages as read
                var unread = chatHistory.Where(m => m.ReceiverEmail == patient.Email && !m.IsRead).ToList();
                if (unread.Any())
                {
                    unread.ForEach(m => m.IsRead = true);
                    _context.SaveChanges();
                }
            }

            ViewBag.ActivePage = "Messages";
            ViewBag.HeaderTitle = "My Messages";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string receiverEmail, string content, IFormFile? file)
        {
            if (!IsPatient()) return Unauthorized();
            var patient = GetLoggedInPatient();
            if (patient == null) return Unauthorized();

            if ((!string.IsNullOrEmpty(content) || file != null) && !string.IsNullOrEmpty(receiverEmail))
            {
                var message = new Message
                {
                    SenderEmail = patient.Email,
                    ReceiverEmail = receiverEmail,
                    Content = content ?? string.Empty,
                    Timestamp = DateTime.Now,
                    IsRead = false
                };

                if (file != null && file.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "messages");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    message.AttachmentPath = "/uploads/messages/" + uniqueFileName;
                    message.AttachmentName = file.FileName;
                }

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Messages), new { receiverEmail = receiverEmail });
        }

        // Helper class for Union
        private class DoctorComparer : System.Collections.Generic.IEqualityComparer<Doctor>
        {
            public bool Equals(Doctor x, Doctor y) => x.Id == y.Id;
            public int GetHashCode(Doctor obj) => obj.Id.GetHashCode();
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
            return View("~/Views/Patient/Settings.cshtml", patient);
        }

        public IActionResult Profile()
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var patient = GetLoggedInPatient();
            if (patient == null) return RedirectToAction("Logout", "Account");

            // Get dynamic stats
            var userName = (string)ViewBag.UserName;
            var appointments = _context.Set<Appointment>().ToList();
            ViewBag.AppointmentCount = appointments.Count(a => a.PatientName == userName);
            ViewBag.RecordCount = 3; // Static for now as per the existing view design, but manageable from controller

            ViewBag.HeaderTitle = "My Profile";
            ViewBag.ActivePage = "Profile";
            ViewBag.UserEmail = patient.Email;
            return View("~/Views/Patient/Profile.cshtml", patient);
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
            SetUserInfo();
            
            var firstName = (string)ViewBag.FirstName;
            var lastName = (string)ViewBag.LastName;
            var fullName = $"{firstName} {lastName}".Trim();

            var appointment = _context.Set<Appointment>().Find(id);
            
            bool isOwnProperty = appointment != null && 
                                (appointment.PatientName != null && 
                                 (appointment.PatientName.Trim().Equals(fullName, StringComparison.OrdinalIgnoreCase) || 
                                  (appointment.PatientName.Contains(firstName, StringComparison.OrdinalIgnoreCase) && 
                                   appointment.PatientName.Contains(lastName, StringComparison.OrdinalIgnoreCase))));

            if (isOwnProperty)
            {
                appointment.Status = "Cancelled";
                _context.SaveChanges();
                TempData["Success"] = "Appointment cancelled successfully.";
            }
            
            return RedirectToAction(nameof(Appointments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAppointment(int id)
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var firstName = (string)ViewBag.FirstName;
            var lastName = (string)ViewBag.LastName;
            var fullName = $"{firstName} {lastName}".Trim();

            var appointment = _context.Set<Appointment>().Find(id);

            bool isOwnProperty = appointment != null && 
                                (appointment.PatientName != null && 
                                 (appointment.PatientName.Trim().Equals(fullName, StringComparison.OrdinalIgnoreCase) || 
                                  (appointment.PatientName.Contains(firstName, StringComparison.OrdinalIgnoreCase) && 
                                   appointment.PatientName.Contains(lastName, StringComparison.OrdinalIgnoreCase))));

            if (isOwnProperty)
            {
                _context.Set<Appointment>().Remove(appointment);
                _context.SaveChanges();
                TempData["Success"] = "Appointment deleted successfully!";
            }

            return RedirectToAction(nameof(Appointments));
        }

        public IActionResult AppointmentDetails(int id)
        {
            if (!IsPatient()) return RedirectToAction("Login", "Account");
            SetUserInfo();

            var appointment = _context.Set<Appointment>().FirstOrDefault(a => a.Id == id);
            
            var firstName = (string)ViewBag.FirstName;
            var lastName = (string)ViewBag.LastName;
            var fullName = $"{firstName} {lastName}".Trim();

            bool isOwnProperty = appointment != null && 
                                (appointment.PatientName != null && 
                                 (appointment.PatientName.Trim().Equals(fullName, StringComparison.OrdinalIgnoreCase) || 
                                  (appointment.PatientName.Contains(firstName, StringComparison.OrdinalIgnoreCase) && 
                                   appointment.PatientName.Contains(lastName, StringComparison.OrdinalIgnoreCase))));

            if (!isOwnProperty)
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

