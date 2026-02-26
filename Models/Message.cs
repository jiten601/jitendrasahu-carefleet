using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CareFleet.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SenderEmail { get; set; } = string.Empty;

        [Required]
        public string ReceiverEmail { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        public string? AttachmentPath { get; set; }
        public string? AttachmentName { get; set; }
        
        // Optional: navigation properties can be tricky without shared base user or specific FKs
        // For simplicity and robustness across different user tables, we use Email as the primary identifier 
        // which matches how the current controllers identify logged-in users.
    }
}
