using System.Diagnostics;
using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace CareFleet.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UserEmail") != null)
            {
                var role = HttpContext.Session.GetString("UserRole");
                var lastPath = Request.Cookies["LastVisitedPath"];

                // If they land on Home while logged in, take them back to where they were or their dashboard
                if (!string.IsNullOrEmpty(lastPath) && !lastPath.Contains("/Account/") && lastPath != "/" && lastPath != "/Home/Index")
                {
                    Response.Cookies.Delete("LastVisitedPath");
                    return Redirect(lastPath);
                }

                return role switch
                {
                    "Admin" => RedirectToAction("Dashboard", "Admin"),
                    "Doctor" => RedirectToAction("Dashboard", "Doctor"),
                    "Patient" => RedirectToAction("Dashboard", "Patient"),
                    _ => View()
                };
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Services()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
