using System;

namespace CareFleet.Models
{
    public class ApplicationUser
    {
        public int Id { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsEmailConfirmed { get; set; }

        public string? EmailOtp { get; set; }
        public DateTime? OtpExpiryTime { get; set; }
    }



}

