using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CareFleet.Models;
using Microsoft.AspNetCore.Http;

namespace CareFleet.Controllers
{
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> MyInvoices()
        {
            SetUserInfo();
            ViewBag.HeaderTitle = "My Invoices";
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Patient") return RedirectToAction("Login", "Account");

            var email = HttpContext.Session.GetString("UserEmail");
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == email);
            if (patient == null) return NotFound();

            var invoices = await _context.Invoices
                .Include(i => i.Appointment)
                .Where(i => i.PatientId == patient.Id)
                .OrderByDescending(i => i.DateGenerated)
                .ToListAsync();

            return View(invoices);
        }

        public async Task<IActionResult> Details(int id)
        {
            SetUserInfo();
            ViewBag.HeaderTitle = "Invoice Details";
            var invoice = await _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Appointment)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (invoice == null) return NotFound();

            // Security check
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userRole == "Patient")
            {
                if (invoice.Patient?.Email != userEmail) return Unauthorized();
            }

            return View(invoice);
        }

        // POST: Billing/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int invoiceId, decimal amount, string method)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Patient") return Json(new { success = false, message = "Unauthorized" });

            var invoice = await _context.Invoices.FindAsync(invoiceId);
            if (invoice == null) return Json(new { success = false, message = "Invoice not found" });

            var payment = new Payment
            {
                InvoiceId = invoiceId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = method,
                TransactionId = "TXN-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper()
            };

            _context.Payments.Add(payment);
            
            // Update invoice status
            var totalPaid = (await _context.Payments.Where(p => p.InvoiceId == invoiceId).SumAsync(p => p.Amount)) + amount;
            if (totalPaid >= invoice.TotalAmount)
            {
                invoice.Status = "Paid";
            }
            else if (totalPaid > 0)
            {
                invoice.Status = "Partially Paid";
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Payment processed successfully!" });
        }

        // POST: Billing/Refund
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Refund(int paymentId, string reason)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Doctor" && userRole != "Admin") 
                return Json(new { success = false, message = "Unauthorized" });

            var payment = await _context.Payments.Include(p => p.Invoice).FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null) return Json(new { success = false, message = "Payment not found" });

            payment.IsRefunded = true;
            payment.RefundDate = DateTime.Now;
            payment.RefundReason = reason;

            // Optional: Reduce total paid amount if you track it on Invoice
            // For now, we'll mark the payment itself.

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Refund processed successfully!" });
        }

        public async Task<IActionResult> AllInvoices()
        {
            SetUserInfo();
            ViewBag.HeaderTitle = "Billing Ledger";
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Admin" && userRole != "Doctor") return RedirectToAction("Login", "Account");

            var invoices = await _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Appointment)
                .OrderByDescending(i => i.DateGenerated)
                .ToListAsync();

            return View(invoices);
        }

        // POST: Billing/CreateAppointmentInvoice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAppointmentInvoice(int appointmentId, decimal amount = 50.00m)
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (userRole != "Doctor" && userRole != "Admin")
                return Json(new { success = false, message = "Unauthorized" });

            // Check if invoice already exists for this appointment
            var existingInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == appointmentId);
            if (existingInvoice != null)
            {
                return Json(new { success = false, message = "An invoice already exists for this appointment." });
            }

            var appointment = await _context.Set<Appointment>().FindAsync(appointmentId);
            if (appointment == null) return Json(new { success = false, message = "Appointment not found." });

            // Find patient by name
            var patient = await _context.Patients.FirstOrDefaultAsync(p => (p.FirstName + " " + p.LastName) == appointment.PatientName);
            if (patient == null) return Json(new { success = false, message = "Patient record not found." });

            try
            {
                await GenerateInvoice(_context, patient.Id, appointmentId, amount);
                return Json(new { success = true, message = "Invoice generated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // Helper to set user info for layout
        private void SetUserInfo()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");
            
            if (!string.IsNullOrEmpty(userEmail))
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                if (user != null)
                {
                    ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
                    ViewBag.FirstName = user.FirstName;
                    ViewBag.LastName = user.LastName;
                    ViewBag.UserRole = userRole;
                    ViewBag.UnreadNotifications = _context.Notifications.Count(n => n.ReceiverEmail == userEmail && !n.IsRead);
                }
            }
        }

        // Helper to generate automated invoice (called from other controllers)
        public static async Task GenerateInvoice(ApplicationDbContext context, int patientId, int appointmentId, decimal baseAmount, decimal discount = 0, string currency = "USD")
        {
            var taxRate = 0.05m; // 5% default tax
            var discountAmount = discount;
            var subtotal = baseAmount - discountAmount;
            var taxAmount = subtotal * taxRate;
            var totalAmount = subtotal + taxAmount;

            var invoice = new Invoice
            {
                PatientId = patientId,
                AppointmentId = appointmentId,
                TotalAmount = totalAmount,
                TaxRate = taxRate,
                TaxAmount = taxAmount,
                DiscountAmount = discountAmount,
                Currency = currency,
                Status = "Unpaid",
                DateGenerated = DateTime.Now,
                DueDate = DateTime.Now.AddDays(7)
            };

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
        }
    }
}
