using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace CareFleet.Controllers
{
    public class AppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AppointmentController(ApplicationDbContext context)
        {
            _context = context;
        }

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

        public IActionResult Index()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            SetUserInfo();
            var appointments = _context.Set<Appointment>()
                .OrderByDescending(a => a.AppointmentDate).ToList();

            ViewBag.ActivePage = "Appointments";
            ViewBag.HeaderTitle = "Appointments";
            return View(appointments);
        }

        public IActionResult Book()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            SetUserInfo();
            var doctors = _context.Doctors.Where(d => d.IsActive).ToList();
            var specialties = doctors.Select(d => d.Specialization).Distinct().ToList();

            ViewBag.Doctors = doctors;
            ViewBag.Specialties = specialties;
            ViewBag.ActivePage = "Appointments";
            ViewBag.HeaderTitle = "Book New Appointment";

            return View();
        }

        [HttpPost]
        public IActionResult Book(Appointment appointment)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(appointment.PatientName))
                {
                    var userEmail = HttpContext.Session.GetString("UserEmail");
                    var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                    if (user != null)
                    {
                        appointment.PatientName = $"{user.FirstName} {user.LastName}".Trim();
                    }
                }

                appointment.CreatedAt = DateTime.Now;
                appointment.Status = "Pending";
                
                _context.Set<Appointment>().Add(appointment);
                _context.SaveChanges();

                // --- Notifications ---
                var bookerEmail = HttpContext.Session.GetString("UserEmail") ?? "";
                var patientName = appointment.PatientName ?? "A patient";
                var appointmentDateStr = appointment.AppointmentDate.ToString("MMMM dd, yyyy 'at' hh:mm tt");

                // 1. Notify the doctor about the new appointment
                // DoctorName is stored as "Dr. FirstName LastName" — find the doctor record
                var doctorNameParts = appointment.DoctorName
                    .Replace("Dr.", "", StringComparison.OrdinalIgnoreCase).Trim().Split(' ');
                var doctor = doctorNameParts.Length >= 2
                    ? _context.Doctors.FirstOrDefault(d =>
                        d.FirstName == doctorNameParts[0] && d.LastName == doctorNameParts[1])
                    : null;

                if (doctor != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        ReceiverEmail = doctor.Email,
                        Message = $"New appointment booked by {patientName} on {appointmentDateStr}.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }

                // 2. Notify the patient/booker with a confirmation
                if (!string.IsNullOrEmpty(bookerEmail))
                {
                    _context.Notifications.Add(new Notification
                    {
                        ReceiverEmail = bookerEmail,
                        Message = $"Your appointment with {appointment.DoctorName} on {appointmentDateStr} has been booked successfully.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }

                _context.SaveChanges();

                TempData["Success"] = "Appointment booked successfully!";
                
                var userRole = HttpContext.Session.GetString("UserRole");
                if (userRole == "Patient")
                {
                    return RedirectToAction("Appointments", "Patient");
                }
                if (userRole == "Admin")
                {
                    return RedirectToAction("Appointments", "Admin");
                }
                return RedirectToAction("Index");
            }

            // If we're here, something failed, redisplay form
            SetUserInfo();
            var doctors = _context.Doctors.Where(d => d.IsActive).ToList();
            var specialties = doctors.Select(d => d.Specialization).Distinct().ToList();

            ViewBag.Doctors = doctors;
            ViewBag.Specialties = specialties;
            ViewBag.ActivePage = "Appointments";
            ViewBag.HeaderTitle = "Book New Appointment";

            return View(appointment);
        }
    }
}
