using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CareFleet.Models;
using Microsoft.AspNetCore.Http;

namespace CareFleet.Controllers
{
    public class PrescriptionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PrescriptionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Prescription/Create?appointmentId=5
        public async Task<IActionResult> Create(int? appointmentId)
        {
            SetUserInfo();
            var userRole = HttpContext.Session.GetString("UserRole");
            if (!string.Equals(userRole, "Doctor", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Login", "Account");

            var doctorEmail = HttpContext.Session.GetString("UserEmail");
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == doctorEmail);
            if (doctor == null) return RedirectToAction("Login", "Account");
            ViewBag.Doctor = doctor;

            if (appointmentId != null)
            {
                var appointment = await _context.Appointments.FindAsync(appointmentId);
                if (appointment == null) return NotFound();

                var fullName = (doctor.FirstName + " " + doctor.LastName).ToLower();
                var apptDoctorName = appointment.DoctorName.ToLower();

                // Flexible check: either direct match or contains (to handle "Dr. " prefix)
                if (!apptDoctorName.Contains(fullName) && !fullName.Contains(apptDoctorName.Replace("dr. ", "").Trim()))
                {
                    TempData["Error"] = "You are not authorized to prescribe for this appointment.";
                    return RedirectToAction("Dashboard", "Doctor");
                }

                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.FirstName + " " + p.LastName == appointment.PatientName);
                if (patient == null) return NotFound();

                ViewBag.Appointment = appointment;
                ViewBag.Patient = patient;
                ViewBag.HeaderTitle = "Prescribe for " + patient.FirstName;
            }
            else
            {
                // Ad-hoc prescription: get all patients this doctor has ever seen
                var patientNames = await _context.Appointments
                    .Where(a => a.DoctorName.Contains(doctor.FirstName) && a.DoctorName.Contains(doctor.LastName))
                    .Select(a => a.PatientName)
                    .Distinct()
                    .ToListAsync();

                // Fetch patients by name matching (standard approach in this project)
                var allPatients = await _context.Patients.ToListAsync();
                var patients = allPatients
                    .Where(p => patientNames.Any(n => n.Contains(p.FirstName) && n.Contains(p.LastName)))
                    .OrderBy(p => p.FirstName)
                    .ToList();

                ViewBag.Patients = patients;
                ViewBag.HeaderTitle = "New Ad-hoc Prescription";
            }

            // Common medications list
            ViewBag.CommonMeds = new List<string> { 
                "Paracetamol 500mg", "Amoxicillin 250mg", "Ibuprofen 400mg", 
                "Metformin 500mg", "Atorvastatin 10mg", "Lisinopril 5mg",
                "Amlodipine 5mg", "Levothyroxine 50mcg", "Omeprazole 20mg",
                "Salbutamol Inhaler", "Cetirizine 10mg", "Azithromycin 500mg"
            };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int patientId, int doctorId, int? appointmentId, string notes, List<PrescriptionItem> items)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (!string.Equals(userRole, "Doctor", StringComparison.OrdinalIgnoreCase)) return Json(new { success = false, message = "Session expired or unauthorized. Please login again." });

            if (items == null || !items.Any())
            {
                return Json(new { success = false, message = "At least one medicine is required." });
            }

            var prescription = new Prescription
            {
                PatientId = patientId,
                DoctorId = doctorId,
                AppointmentId = appointmentId,
                Notes = notes,
                DateCreated = DateTime.Now
            };

            _context.Prescriptions.Add(prescription);
            await _context.SaveChangesAsync();

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.MedicineName)) continue;
                    
                    item.PrescriptionId = prescription.Id;
                    _context.PrescriptionItems.Add(item);
                }
                await _context.SaveChangesAsync();

                // Send notification to patient
                var patient = await _context.Patients.FindAsync(patientId);
                var doctor = await _context.Doctors.FindAsync(doctorId);
                if (patient != null && doctor != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        ReceiverEmail = patient.Email,
                        Message = $"Dr. {doctor.FirstName} {doctor.LastName} has issued a new prescription for you.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Success"] = "Prescription created successfully!";
            return Json(new { success = true, redirectUrl = Url.Action("Dashboard", "Doctor") });
        }

        // GET: Prescription/MyPrescriptions (For both Doctors and Patients)
        public async Task<IActionResult> MyPrescriptions()
        {
            SetUserInfo();
            var userRole = HttpContext.Session.GetString("UserRole");
            ViewBag.HeaderTitle = userRole == "Doctor" ? "Prescription History" : "My Prescriptions";
            var email = HttpContext.Session.GetString("UserEmail");
            
            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(email)) 
                return RedirectToAction("Login", "Account");

            IQueryable<Prescription> query = _context.Prescriptions
                .Include(p => p.Doctor)
                .Include(p => p.Patient)
                .Include(p => p.Items);

            if (userRole == "Patient")
            {
                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == email);
                if (patient == null) return NotFound();
                query = query.Where(p => p.PatientId == patient.Id);
            }
            else if (userRole == "Doctor")
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == email);
                if (doctor == null) return NotFound();
                query = query.Where(p => p.DoctorId == doctor.Id);
            }
            else
            {
                return RedirectToAction("Login", "Account");
            }

            var prescriptions = await query
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();

            return View(prescriptions);
        }

        // Helper to set user info for layout
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
                    ViewBag.UnreadNotifications = _context.Notifications.Count(n => n.ReceiverEmail == userEmail && !n.IsRead);
                }
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            SetUserInfo();
            ViewBag.HeaderTitle = "Prescription Details";
            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .Include(p => p.Patient)
                .Include(p => p.Items)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (prescription == null) return NotFound();

            // Security check
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userRole == "Patient")
            {
                if (!string.Equals(prescription.Patient?.Email, userEmail, StringComparison.OrdinalIgnoreCase)) 
                {
                    TempData["Error"] = "You are not authorized to view this prescription.";
                    return RedirectToAction("MyPrescriptions");
                }
            }
            else if (userRole == "Doctor")
            {
                if (!string.Equals(prescription.Doctor?.Email, userEmail, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "You do not have permission to view prescriptions for this patient.";
                    return RedirectToAction("MyPrescriptions");
                }
            }

            return View(prescription);
        }
    }
}
