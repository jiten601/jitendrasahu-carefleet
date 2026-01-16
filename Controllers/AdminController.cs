using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;

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
            if (string.IsNullOrEmpty(userEmail))
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
            var pendingAppointments = 45; // Placeholder - can be updated when Appointments table is added

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
    }
}

