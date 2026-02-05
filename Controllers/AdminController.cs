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
            var pendingAppointments = _context.Appointments.Count(a => a.Status == "Pending");

            ViewBag.UserName = userName;
            ViewBag.FirstName = user.FirstName;
            ViewBag.LastName = user.LastName;
            ViewBag.HeaderTitle = "Dashboard";
            ViewBag.ActivePage = "Dashboard";
            ViewBag.TotalUsers = totalUsers;
            ViewBag.ActiveDoctors = activeDoctors;
            ViewBag.PendingAppointments = pendingAppointments;

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
            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .OrderByDescending(a => a.AppointmentDate)
                .ToList();

            ViewBag.HeaderTitle = "Appointments Management";
            ViewBag.ActivePage = "Appointments";
            return View(appointments);
        }

        public IActionResult Reports()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            SetUserInfo();
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
            }
        }
    }
}

