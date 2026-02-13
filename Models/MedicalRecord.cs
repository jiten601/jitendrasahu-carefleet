using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareFleet.Models
{
    public class MedicalRecord
    {
        [Key]
        public int Id { get; set; }

        public int PatientId { get; set; }

        [ForeignKey("PatientId")]
        public virtual Patient? Patient { get; set; }

        [Required]
        [StringLength(200)]
        public string DocumentName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty; // Laboratory, Radiology, General Health, etc.

        public DateTime DateIssued { get; set; }

        [StringLength(200)]
        public string Provider { get; set; } = string.Empty;

        [StringLength(500)]
        public string? FilePath { get; set; }

        [StringLength(50)]
        public string? FileType { get; set; } // PDF, JPG, DICOM, etc.
    }
}
