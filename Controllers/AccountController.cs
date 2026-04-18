using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using CareFleet.Services;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Linq;


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
            
            // Clear the cookie once read to prevent infinite redirect loops across modules
            Response.Cookies.Delete("LastVisitedPath");

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

            if (user.Role == "Doctor")
            {
                var doctor = _context.Doctors.FirstOrDefault(d => d.Email == user.Email);
                if (doctor != null && !doctor.IsActive)
                {
                    ViewBag.Error = "Your account is pending for admin approval.";
                    return View();
                }
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

            if (model.Email.Equals("jitenshah601@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                model.Role = "Admin";
            }

            if (_context.Users.Any(u => u.Email == model.Email) || 
                _context.Doctors.Any(d => d.Email == model.Email) || 
                _context.Patients.Any(p => p.Email == model.Email))
            {
                ViewBag.Error = "Email already exists in our system";
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
                IsEmailConfirmed = false,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);

            // Register as entity
            if (model.Role == "Doctor")
            {
                // Validate doctor-specific required fields
                if (string.IsNullOrEmpty(model.Specialization) || string.IsNullOrEmpty(model.LicenseNumber) || string.IsNullOrEmpty(model.PhoneNumber))
                {
                    ViewBag.Error = "Please fill all required doctor details.";
                    return View(model);
                }

                var doctor = new Doctor
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    Specialization = model.Specialization,
                    LicenseNumber = model.LicenseNumber,
                    IsActive = false,
                    CreatedAt = DateTime.Now
                };
                _context.Doctors.Add(doctor);
            }
            else if (model.Role == "Patient")
            {
                // Validate patient-specific required fields
                if (string.IsNullOrEmpty(model.PhoneNumber) || model.DateOfBirth == null ||
                    string.IsNullOrEmpty(model.Gender) || string.IsNullOrEmpty(model.BloodGroup) ||
                    string.IsNullOrEmpty(model.Address) || string.IsNullOrEmpty(model.MedicalHistory))
                {
                    ViewBag.Error = "Please fill all required patient details.";
                    return View(model);
                }

                var patient = new Patient
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    DateOfBirth = model.DateOfBirth.Value,
                    Gender = model.Gender,
                    Address = model.Address,
                    BloodGroup = model.BloodGroup,
                    MedicalHistory = model.MedicalHistory,
                    Allergies = model.Allergies,
                    CreatedAt = DateTime.Now
                };
                _context.Patients.Add(patient);
            }

            _context.SaveChanges();

            // Notify all admins about the new registration
            var adminEmails = _context.Users
                .Where(u => u.Role == "Admin")
                .Select(u => u.Email)
                .ToList();

            var roleLabel = model.Role == "Doctor" ? "Doctor" : model.Role == "Patient" ? "Patient" : "User";
            var fullName = $"{model.FirstName} {model.LastName}".Trim();
            var adminMessage = $"New {roleLabel} registered: {fullName} ({model.Email}).";

            foreach (var adminEmail in adminEmails)
            {
                _context.Notifications.Add(new Notification
                {
                    ReceiverEmail = adminEmail,
                    Message = adminMessage,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                // Send email notification to admins for new Doctor registrations
                if (model.Role == "Doctor")
                {
                    try
                    {
                        var subject = "Action Required: New Doctor Registered";
                        var body = $@"
                            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                                <h2 style='color: #1B3C53;'>New Doctor Registered</h2>
                                <p>A new doctor has registered on CareFleet and is <b>waiting for your approval</b>.</p>
                                <div style='background: #f8f9fa; padding: 15px; border-radius: 8px; border-left: 4px solid #4299E1; margin: 20px 0;'>
                                    <p style='margin: 5px 0;'><strong>Name:</strong> {fullName}</p>
                                    <p style='margin: 5px 0;'><strong>Email:</strong> {model.Email}</p>
                                    <p style='margin: 5px 0;'><strong>Specialization:</strong> {model.Specialization}</p>
                                    <p style='margin: 5px 0;'><strong>License No:</strong> {model.LicenseNumber}</p>
                                </div>
                                <p>Please log in to the <a href='https://carefleet-fyp-2026-fccudzeehhc0dsg2.centralindia-01.azurewebsites.net/Admin/Dashboard' style='color: #4299E1; font-weight: bold;'>Admin Dashboard</a> to review and approve this account.</p>
                                <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                                <p style='font-size: 0.8em; color: #777;'>This is an automated notification from CareFleet Hospital Management System.</p>
                            </div>";
                        _emailService.Send(adminEmail, subject, body);
                    }
                    catch (Exception ex)
                    {
                        // Silent fail for email to prevent registration crash, 
                        // but ideally we would log this.
                    }
                }
            }
            if (adminEmails.Any()) _context.SaveChanges();

            _emailService.Send(
                model.Email,
                "CareFleet Email Verification OTP",
                $"Your OTP code is <b>{otp}</b>. It is valid for 10 minutes."
            );

            TempData["Email"] = model.Email;
            return RedirectToAction("VerifyOtp");
        }

        // FORGOT PASSWORD
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

            if (user == null)
            {
                // We show a success message even if the user doesn't exist to prevent email enumeration
                ViewBag.Message = "If an account exists with this email, an OTP has been sent.";
                return View();
            }

            var otp = GenerateOtp();
            user.EmailOtp = otp;
            user.OtpExpiryTime = DateTime.Now.AddMinutes(10);
            _context.SaveChanges();

            _emailService.Send(
                model.Email,
                "CareFleet Password Reset OTP",
                $"Your password reset OTP code is <b>{otp}</b>. It is valid for 10 minutes."
            );

            return RedirectToAction("ResetPassword", new { email = model.Email });
        }

        // RESET PASSWORD
        public IActionResult ResetPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            var model = new ResetPasswordViewModel { Email = email };
            return View(model);
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email && u.EmailOtp == model.Otp);

            if (user == null || user.OtpExpiryTime < DateTime.Now)
            {
                ViewBag.Error = "Invalid or expired OTP";
                return View(model);
            }

            // Update password
            user.PasswordHash = HashPassword(model.NewPassword);
            user.EmailOtp = null;
            user.OtpExpiryTime = null;

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Password has been reset successfully. Please login with your new password.";
            return RedirectToAction("Login");
        }


        private void SetUserInfo()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (!string.IsNullOrEmpty(userEmail))
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                if (user != null)
                {
                    ViewBag.FirstName = user.FirstName;
                    ViewBag.LastName = user.LastName;
                    ViewBag.UserRole = user.Role;
                    ViewBag.UnreadNotifications = _context.Notifications.Count(n => n.ReceiverEmail == userEmail && !n.IsRead);
                }
            }
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                return RedirectToAction("Login");
            }
            SetUserInfo();
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword([FromBody] ChangePasswordViewModel model)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                return Json(new { success = false, message = "Session expired. Please login again." });
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            var currentHash = HashPassword(model.CurrentPassword);
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail && u.PasswordHash == currentHash);

            if (user == null)
            {
                return Json(new { success = false, message = "Incorrect current password." });
            }

            user.PasswordHash = HashPassword(model.NewPassword);
            _context.SaveChanges();

            return Json(new { success = true, message = "Password updated successfully." });
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




        private async Task SignInUser(ApplicationUser user)
        {
            // Set Session
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("SessionCreatedAt", DateTime.Now.ToString("o"));

            // Cookie Auth (for Persistent login or just basic auth context)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", $"{user.FirstName} {user.LastName}")
            };

            var identity = new ClaimsIdentity(claims, "CareFleetAuth");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("CareFleetAuth", principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTime.UtcNow.AddDays(30)
            });
        }
    }
}

