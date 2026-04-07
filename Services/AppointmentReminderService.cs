using CareFleet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CareFleet.Services
{
    public class AppointmentReminderService : BackgroundService
    {
        private readonly ILogger<AppointmentReminderService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public AppointmentReminderService(ILogger<AppointmentReminderService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing appointment reminders.");
                }

                // Check every 5 minutes
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task ProcessRemindersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

            // Get pending or confirmed appointments that have a patient email and where a reminder hasn't been sent yet
            var now = DateTime.Now;
            
            var upcomingAppointments = await context.Appointments
                .Where(a => a.Status != "Cancelled" && a.Status != "Rejected" 
                            && !string.IsNullOrEmpty(a.PatientEmail)
                            && (!a.Is24HourReminderSent || !a.Is1HourReminderSent))
                .ToListAsync();

            foreach (var appt in upcomingAppointments)
            {
                // Try to parse the exact appointment time
                if (TryParseAppointmentDateTime(appt.AppointmentDate, appt.TimeSlot, out DateTime targetTime))
                {
                    var timeDiff = targetTime - now;

                    // 1. Check for 24-Hour Reminder (Between 24h and 23h away)
                    if (timeDiff.TotalHours <= 24 && timeDiff.TotalHours > 23 && !appt.Is24HourReminderSent)
                    {
                        await SendReminder(context, emailService, appt, targetTime, is24Hour: true);
                        appt.Is24HourReminderSent = true;
                    }

                    // 2. Check for 1-Hour Reminder (Between 1h and 0h away)
                    if (timeDiff.TotalHours <= 1 && timeDiff.TotalHours > 0 && !appt.Is1HourReminderSent)
                    {
                        await SendReminder(context, emailService, appt, targetTime, is24Hour: false);
                        appt.Is1HourReminderSent = true;
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        private bool TryParseAppointmentDateTime(DateTime date, string timeSlot, out DateTime parsedDateTime)
        {
            parsedDateTime = DateTime.MinValue;
            try
            {
                // Assuming TimeSlot format is "hh:mm tt" e.g., "09:00 AM" or "02:00 PM"
                var dateTimeStr = $"{date:yyyy-MM-dd} {timeSlot}";
                if (DateTime.TryParseExact(dateTimeStr, "yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDateTime))
                {
                    return true;
                }
            }
            catch
            {
                // Ignored
            }
            return false;
        }

        private async Task SendReminder(ApplicationDbContext context, EmailService emailService, Appointment appt, DateTime targetTime, bool is24Hour)
        {
            string timeFrame = is24Hour ? "24 hours" : "1 hour";
            string subject = $"CareFleet Reminder: Appointment in {timeFrame}";
            string patientName = string.IsNullOrEmpty(appt.PatientName) ? "Patient" : appt.PatientName;
            
            string body = $@"
                <h3>Appointment Reminder</h3>
                <p>Dear {patientName},</p>
                <p>This is a friendly reminder that you have an upcoming appointment with {appt.DoctorName} in approximately {timeFrame}.</p>
                <p><strong>Details:</strong></p>
                <ul>
                    <li><strong>Doctor:</strong> {appt.DoctorName}</li>
                    <li><strong>Date:</strong> {targetTime:dddd, MMMM dd, yyyy}</li>
                    <li><strong>Time:</strong> {targetTime:hh:mm tt}</li>
                    <li><strong>Reason:</strong> {appt.Reason}</li>
                </ul>
                <p>Please log in to your CareFleet account if you need to review details or manage your appointment.</p>
                <p>Stay healthy,<br/>CareFleet Team</p>
            ";

            // 1. Send Email
            try
            {
                // We use ThreadPool/Task.Run if EmailService is synchronous to avoid blocking
                emailService.Send(appt.PatientEmail!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email reminder to {appt.PatientEmail}");
            }

            // 2. Add System Notification
            var notification = new Notification
            {
                ReceiverEmail = appt.PatientEmail!,
                Message = $"REMINDER: Your appointment with {appt.DoctorName} is in {timeFrame} ({targetTime:MMM dd at hh:mm tt}).",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            await context.Notifications.AddAsync(notification);
        }
    }
}
