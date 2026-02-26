using System;

namespace CareFleet.Models
{
    public class HealthTip
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Icon { get; set; } = "fas fa-lightbulb";
        public string ColorClass { get; set; } = "text-primary";
    }
}
