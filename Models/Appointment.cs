using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareFleet.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public int DoctorId { get; set; }
        
        [ForeignKey("DoctorId")]
        public virtual Doctor? Doctor { get; set; }

        public int? PatientId { get; set; }
        
        [ForeignKey("PatientId")]
        public virtual Patient? Patient { get; set; }

        [Required]
        public DateTime AppointmentDate { get; set; }

        [Required]
        [StringLength(50)]
        public string TimeSlot { get; set; } = string.Empty;

        [Required]
        public string Reason { get; set; } = string.Empty;

        public bool IsForSomeoneElse { get; set; }

        public string? OtherPatientName { get; set; }

        public DateTime? OtherPatientDOB { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
