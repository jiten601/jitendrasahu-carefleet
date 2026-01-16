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
        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string email, string password)
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
            return RedirectToAction("Dashboard", "Admin");
        }

        // REGISTER
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(string FirstName, string LastName, string Email, string PasswordHash, string confirmPassword)
        {
            if (PasswordHash != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            if (_context.Users.Any(u => u.Email == Email))
            {
                ViewBag.Error = "Email already exists";
                return View();
            }

            var otp = GenerateOtp();

            var user = new ApplicationUser
            {
                FirstName = FirstName,
                LastName = LastName,
                Email = Email,
                PasswordHash = HashPassword(PasswordHash),
                EmailOtp = otp,
                OtpExpiryTime = DateTime.Now.AddMinutes(10),
                IsEmailConfirmed = false
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            _emailService.Send(
                Email,
                "CareFleet Email Verification OTP",
                $"Your OTP code is <b>{otp}</b>. It is valid for 10 minutes."
            );

            TempData["Email"] = Email;
            return RedirectToAction("VerifyOtp");
        }

        // LOGOUT
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
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

