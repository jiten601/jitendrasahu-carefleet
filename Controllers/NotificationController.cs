using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace CareFleet.Controllers
{
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetNotifications()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            var userRole = HttpContext.Session.GetString("UserRole") ?? "";

            var notifications = _context.Notifications
                .Where(n => n.ReceiverEmail == userEmail)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToList()
                .Select(n => new {
                    n.Id,
                    n.Message,
                    n.IsRead,
                    CreatedAt = n.CreatedAt.ToString("MMM dd, HH:mm"),
                    TargetUrl = DeriveUrl(n.Message, userRole)
                })
                .ToList();

            return Json(notifications);
        }

        /// <summary>
        /// Derives a navigation URL from the notification message text and the current user's role.
        /// </summary>
        private static string DeriveUrl(string message, string role)
        {
            var msg = message.ToLowerInvariant();

            // Message / chat notifications
            if (msg.Contains("sent you a message") || msg.Contains("new message"))
            {
                return role == "Doctor" ? "/Doctor/Messages" : "/Patient/Messages";
            }

            // Medical record notifications
            if (msg.Contains("medical record") || msg.Contains("new record"))
            {
                return role == "Doctor" ? "/Doctor/MedicalRecords" : "/Patient/MedicalRecords";
            }

            // Appointment notifications
            if (msg.Contains("appointment"))
            {
                return role == "Doctor" ? "/Doctor/Appointments" : "/Patient/Appointments";
            }

            // Prescription notifications
            if (msg.Contains("prescription"))
            {
                return role == "Doctor" ? "/Prescription/MyPrescriptions" : "/Prescription/MyPrescriptions";
            }

            // Default to dashboard
            return role == "Doctor" ? "/Doctor/Dashboard" : "/Patient/Dashboard";
        }

        [HttpPost]
        public IActionResult MarkAsRead(int id)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            var notification = _context.Notifications.FirstOrDefault(n => n.Id == id && n.ReceiverEmail == userEmail);
            if (notification != null)
            {
                notification.IsRead = true;
                _context.SaveChanges();
                return Ok();
            }
            return NotFound();
        }

        [HttpPost]
        public IActionResult MarkAllAsRead()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            var unread = _context.Notifications.Where(n => n.ReceiverEmail == userEmail && !n.IsRead).ToList();
            unread.ForEach(n => n.IsRead = true);
            _context.SaveChanges();
            return Ok();
        }
    }
}
