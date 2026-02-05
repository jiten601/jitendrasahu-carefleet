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

        public IActionResult Index()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            var appointments = _context.Set<Appointment>()
                .Include(a => a.Doctor).Include(a => a.Patient).OrderByDescending(a => a.AppointmentDate).ToList();

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
                appointment.CreatedAt = DateTime.Now;
                appointment.Status = "Pending";
                
                _context.Set<Appointment>().Add(appointment);
                _context.SaveChanges();

                TempData["Success"] = "Appointment booked successfully!";
                return RedirectToAction("Index");
            }

            // If we're here, something failed, redisplay form
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
