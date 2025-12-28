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

            var userName = HttpContext.Session.GetString("UserName") ?? "Admin";

            // Get dashboard statistics
            var totalUsers = _context.Users.Count();
            var activeDoctors = 120; // Placeholder - can be updated when Doctors table is added
            var pendingAppointments = 45; // Placeholder - can be updated when Appointments table is added

            ViewBag.UserName = userName;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.ActiveDoctors = activeDoctors;
            ViewBag.PendingAppointments = pendingAppointments;

            return View();
        }
    }
}

