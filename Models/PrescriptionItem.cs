using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareFleet.Models
{
    public class PrescriptionItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PrescriptionId { get; set; }

        [Required]
        [MaxLength(200)]
        public string MedicineName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Dosage { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Frequency { get; set; } = string.Empty; // e.g., Twice a day

        [Required]
        [MaxLength(100)]
        public string Duration { get; set; } = string.Empty; // e.g., 7 days

        public string? Instructions { get; set; } // e.g., After meals
        
        public int Refills { get; set; } = 0;
        
        public DateTime? ExpiryDate { get; set; }

        // Navigation property
        [ForeignKey("PrescriptionId")]
        public virtual Prescription? Prescription { get; set; }
    }
}
