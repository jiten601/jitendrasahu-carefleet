using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;

namespace CareFleet.Controllers
{
    public class DoctorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DoctorController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Check authentication helper
        private bool IsAuthenticated()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            return !string.IsNullOrEmpty(userEmail);
        }

        // Helper to set user info
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
                }
            }
        }

        // GET: Doctor
        public IActionResult Index()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Doctors Management";
            ViewBag.ActivePage = "Doctors";
            var doctors = _context.Doctors.OrderByDescending(d => d.CreatedAt).ToList();
            return View(doctors);
        }

        // GET: Doctor/Create
        public IActionResult Create()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }
            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Doctor";
            ViewBag.ActivePage = "Doctors";
            return View();
        }

        // POST: Doctor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Doctor doctor)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                if (_context.Doctors.Any(d => d.Email == doctor.Email))
                {
                    SetUserInfo();
                    ViewBag.HeaderTitle = "Add New Doctor";
                    ViewBag.ActivePage = "Doctors";
                    ViewBag.Error = "A doctor with this email already exists.";
                    return View(doctor);
                }

                if (_context.Doctors.Any(d => d.LicenseNumber == doctor.LicenseNumber))
                {
                    SetUserInfo();
                    ViewBag.HeaderTitle = "Add New Doctor";
                    ViewBag.ActivePage = "Doctors";
                    ViewBag.Error = "A doctor with this license number already exists.";
                    return View(doctor);
                }

                doctor.CreatedAt = DateTime.Now;
                _context.Doctors.Add(doctor);
                _context.SaveChanges();
                TempData["Success"] = "Doctor added successfully!";
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Doctor";
            ViewBag.ActivePage = "Doctors";
            return View(doctor);
        }

        // GET: Doctor/Edit/5
        public IActionResult Edit(int? id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null)
            {
                return NotFound();
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Edit Doctor";
            ViewBag.ActivePage = "Doctors";
            var doctor = _context.Doctors.Find(id);
            if (doctor == null)
            {
                return NotFound();
            }

            return View(doctor);
        }

        // POST: Doctor/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Doctor doctor)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != doctor.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingDoctor = _context.Doctors.FirstOrDefault(d => d.Email == doctor.Email && d.Id != id);
                if (existingDoctor != null)
                {
                    ViewBag.Error = "A doctor with this email already exists.";
                    return View(doctor);
                }

                existingDoctor = _context.Doctors.FirstOrDefault(d => d.LicenseNumber == doctor.LicenseNumber && d.Id != id);
                if (existingDoctor != null)
                {
                    SetUserInfo();
                    ViewBag.HeaderTitle = "Edit Doctor";
                    ViewBag.ActivePage = "Doctors";
                    ViewBag.Error = "A doctor with this license number already exists.";
                    return View(doctor);
                }

                doctor.UpdatedAt = DateTime.Now;
                _context.Update(doctor);
                _context.SaveChanges();
                TempData["Success"] = "Doctor updated successfully!";
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Edit Doctor";
            ViewBag.ActivePage = "Doctors";
            return View(doctor);
        }

        // POST: Doctor/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = _context.Doctors.Find(id);
            if (doctor != null)
            {
                _context.Doctors.Remove(doctor);
                _context.SaveChanges();
                TempData["Success"] = "Doctor deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Doctor/ToggleStatus/5
        [HttpPost]
        public IActionResult ToggleStatus(int id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = _context.Doctors.Find(id);
            if (doctor != null)
            {
                doctor.IsActive = !doctor.IsActive;
                doctor.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

