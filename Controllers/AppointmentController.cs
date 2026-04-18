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
        private readonly CareFleet.Services.EmailService _emailService;

        public AppointmentController(ApplicationDbContext context, CareFleet.Services.EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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
                // Check for existing appointment for the same doctor, date, and time slot
                bool isSlotTaken = _context.Set<Appointment>().Any(a =>
                    a.DoctorName == appointment.DoctorName &&
                    a.AppointmentDate.Date == appointment.AppointmentDate.Date &&
                    a.TimeSlot == appointment.TimeSlot &&
                    a.Status != "Cancelled" && a.Status != "Rejected");

                if (isSlotTaken)
                {
                    ModelState.AddModelError("TimeSlot", "This time slot is already booked for the selected doctor. Please choose another time.");

                    SetUserInfo();
                    var activeDoctors = _context.Doctors.Where(d => d.IsActive).ToList();
                    var activeSpecialties = activeDoctors.Select(d => d.Specialization).Distinct().ToList();

                    ViewBag.Doctors = activeDoctors;
                    ViewBag.Specialties = activeSpecialties;
                    ViewBag.ActivePage = "Appointments";
                    ViewBag.HeaderTitle = "Book New Appointment";

                    return View(appointment);
                }

                var bookerEmail = HttpContext.Session.GetString("UserEmail");
                appointment.PatientEmail = bookerEmail;

                if (string.IsNullOrEmpty(appointment.PatientName))
                {
                    var user = _context.Users.FirstOrDefault(u => u.Email == bookerEmail);
                    if (user != null)
                    {
                        appointment.PatientName = $"{user.FirstName} {user.LastName}".Trim();
                    }
                }

                appointment.CreatedAt = DateTime.Now;
                appointment.Status = "Pending";
                appointment.Fee = 200;

                _context.Set<Appointment>().Add(appointment);
                _context.SaveChanges();

                // --- Notifications ---
                bookerEmail = bookerEmail ?? "";
                var patientName = appointment.PatientName ?? "A patient";
                var appointmentDateStr = $"{appointment.AppointmentDate:MMMM dd, yyyy} at {appointment.TimeSlot}";

                // 1. Notify the doctor about the new appointment
                var doctor = _context.Doctors.ToList()
                    .FirstOrDefault(d => $"Dr. {d.FirstName} {d.LastName}" == appointment.DoctorName);

                if (doctor != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        ReceiverEmail = doctor.Email,
                        Message = $"New appointment booked by {patientName} on {appointmentDateStr}.",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                    
                    try
                    {
                        var subject = "New Appointment Request - CareFleet";
                        var body = $@"
                            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                                <h2 style='color: #2B6CB0;'>New Appointment Request</h2>
                                <p>Hello Dr. {doctor.FirstName} {doctor.LastName},</p>
                                <p>You have received a new appointment request from <strong>{patientName}</strong> for <strong>{appointmentDateStr}</strong>.</p>
                                <p>Please log in to your dashboard to review and confirm this request.</p>
                                <div style='margin: 25px 0;'>
                                    <a href='https://carefleet-fyp-2026-fccudzeehhc0dsg2.centralindia-01.azurewebsites.net/Doctor/Appointments' 
                                       style='background-color: #4299E1; color: white; padding: 12px 25px; text-decoration: none; border-radius: 8px; font-weight: bold;'>
                                        Manage Appointments
                                    </a>
                                </div>
                                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                                <p style='font-size: 0.8em; color: #777;'>CareFleet automated scheduling.</p>
                            </div>";
                        _emailService.Send(doctor.Email, subject, body);
                    }
                    catch { /* Silent fail for email */ }
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
