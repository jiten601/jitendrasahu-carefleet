using CareFleet.Models;
using Microsoft.AspNetCore.Mvc;

namespace CareFleet.Controllers
{
    public class PatientController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PatientController(ApplicationDbContext context)
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

        // GET: Patient
        public IActionResult Index()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Patients Management";
            ViewBag.ActivePage = "Patients";
            var patients = _context.Patients.OrderByDescending(p => p.CreatedAt).ToList();
            return View(patients);
        }

        // GET: Patient/Create
        public IActionResult Create()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }
            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Patient";
            ViewBag.ActivePage = "Patients";
            return View();
        }

        // POST: Patient/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Patient patient)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                patient.CreatedAt = DateTime.Now;
                _context.Patients.Add(patient);
                _context.SaveChanges();
                TempData["Success"] = "Patient added successfully!";
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Add New Patient";
            ViewBag.ActivePage = "Patients";
            return View(patient);
        }

        // GET: Patient/Edit/5
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
            ViewBag.HeaderTitle = "Edit Patient";
            ViewBag.ActivePage = "Patients";
            var patient = _context.Patients.Find(id);
            if (patient == null)
            {
                return NotFound();
            }

            return View(patient);
        }

        // POST: Patient/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Patient patient)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            if (id != patient.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                patient.UpdatedAt = DateTime.Now;
                _context.Update(patient);
                _context.SaveChanges();
                TempData["Success"] = "Patient updated successfully!";
                return RedirectToAction(nameof(Index));
            }

            SetUserInfo();
            ViewBag.HeaderTitle = "Edit Patient";
            ViewBag.ActivePage = "Patients";
            return View(patient);
        }

        // POST: Patient/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            var patient = _context.Patients.Find(id);
            if (patient != null)
            {
                _context.Patients.Remove(patient);
                _context.SaveChanges();
                TempData["Success"] = "Patient deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

