using System;

namespace CareFleet.Models
{
    public class ApplicationUser
    {
        public int Id { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public bool IsEmailConfirmed { get; set; }

        public string? EmailOtp { get; set; }
        public DateTime? OtpExpiryTime { get; set; }
    }



}

