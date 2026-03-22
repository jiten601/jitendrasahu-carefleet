using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareFleet.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InvoiceId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(100)]
        public string PaymentMethod { get; set; } = "Cash"; // Cash, Card, Online, Insurance

        public string? TransactionId { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
        
        public bool IsRefunded { get; set; } = false;
        public DateTime? RefundDate { get; set; }
        public string? RefundReason { get; set; }

        // Navigation property
        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }
    }
}
