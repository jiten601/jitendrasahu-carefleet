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

                    // Fetch unread notifications if patient
                    var patient = _context.Patients.FirstOrDefault(p => p.Email == userEmail);
                    if (patient != null)
                    {
                        ViewBag.UnreadNotifications = _context.Notifications.Count(n => n.PatientId == patient.Id && !n.IsRead);
                    }
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
