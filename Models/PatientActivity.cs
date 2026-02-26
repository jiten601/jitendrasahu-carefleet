using System;

namespace CareFleet.Models
{
    public class PatientActivity
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Icon { get; set; } = "fas fa-info-circle";
        public string Type { get; set; } = "General"; // Appointment, Message, MedicalRecord, Account
    }
}
