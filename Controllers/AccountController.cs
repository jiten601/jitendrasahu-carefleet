using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using CareFleet.Services;


namespace CareFleet.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;


        public AccountController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // OTP GENERATION
        private string GenerateOtp()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }


        // VERIFY OTP
        public IActionResult VerifyOtp() => View();

        [HttpPost]
        public IActionResult VerifyOtp(string otp)
        {
            var user = _context.Users.FirstOrDefault(u => u.EmailOtp == otp);

            if (user == null)
            {
                ViewBag.Error = "Invalid OTP";
                return View();
            }

            user.IsEmailConfirmed = true;
            user.EmailOtp = null; // ? now safe if nullable
            user.OtpExpiryTime = null; // optional

            _context.SaveChanges();

            return RedirectToAction("Login");
        }



        // LOGIN
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserEmail") != null)
            {
                return GetRoleBasedRedirect();
            }
            return View();
        }

        private IActionResult GetRoleBasedRedirect()
        {
            var role = HttpContext.Session.GetString("UserRole");
            var lastPath = Request.Cookies["LastVisitedPath"];

            // If we have a last path and it's not a generic one, try to use it
            if (!string.IsNullOrEmpty(lastPath) && !lastPath.Contains("/Account/"))
            {
                return Redirect(lastPath);
            }

            return role switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Doctor" => RedirectToAction("Dashboard", "Doctor"),
                "Patient" => RedirectToAction("Dashboard", "Patient"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        [HttpPost]
        public IActionResult Login(string email, string password, bool rememberMe)
        {
            var hash = HashPassword(password);
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.PasswordHash == hash);

            if (user == null)
            {
                ViewBag.Error = "Invalid Email or Password";
                return View();
            }

            if (!user.IsEmailConfirmed)
            {
                ViewBag.Error = "Please verify your email before logging in";
                return View();
            }

            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.FirstName + " " + user.LastName);
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("SessionCreatedAt", DateTime.Now.ToString("o"));

            // Handle Remember Me
            if (rememberMe)
            {
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax
                };
                Response.Cookies.Append("CareFleetAuth", user.Email, cookieOptions);
            }

            // Role-based redirection
            return GetRoleBasedRedirect();
        }

        // REGISTER
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ViewBag.Error = "Email already exists";
                return View(model);
            }

            var otp = GenerateOtp();

            var user = new ApplicationUser
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                PasswordHash = HashPassword(model.Password),
                Role = model.Role,
                EmailOtp = otp,
                OtpExpiryTime = DateTime.Now.AddMinutes(10),
                IsEmailConfirmed = false
            };

            _context.Users.Add(user);

            // Register as entity
            if (model.Role == "Doctor")
            {
                var doctor = new Doctor
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _context.Doctors.Add(doctor);
            }
            else if (model.Role == "Patient")
            {
                var patient = new Patient
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    CreatedAt = DateTime.Now
                };
                _context.Patients.Add(patient);
            }

            _context.SaveChanges();

            _emailService.Send(
                model.Email,
                "CareFleet Email Verification OTP",
                $"Your OTP code is <b>{otp}</b>. It is valid for 10 minutes."
            );

            TempData["Email"] = model.Email;
            return RedirectToAction("VerifyOtp");
        }

        // LOGOUT
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("CareFleetAuth");
            Response.Cookies.Delete("LastVisitedPath");
            return RedirectToAction("Login");
        }

        private string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}

