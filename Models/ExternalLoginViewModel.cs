using System.ComponentModel.DataAnnotations;

namespace CareFleet.Models
{
    public class ExternalLoginViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        // --- Common ---
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        // --- Patient Specific ---
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? BloodGroup { get; set; }
        public string? MedicalHistory { get; set; }
        public string? Allergies { get; set; }

        // --- Doctor Specific ---
        public string? Specialization { get; set; }
        public string? LicenseNumber { get; set; }
        public string GoogleId { get; internal set; }
    }
}
