using System.ComponentModel.DataAnnotations;

namespace CareFleet.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Doctor Name")]
        public string DoctorName { get; set; }

        [Display(Name = "Patient Name")]
        public string? PatientName { get; set; }

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