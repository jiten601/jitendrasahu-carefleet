using System.Net;
using System.Net.Mail;

namespace CareFleet.Services
{
    public class EmailService
    {
        public void Send(string toEmail, string subject, string body)
        {
            var fromAddress = new MailAddress("jis202406@gmail.com", "CareFleet");

            var message = new MailMessage();
            message.From = fromAddress;
            message.To.Add(toEmail);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using (var smtp = new SmtpClient("smtp.gmail.com", 587))
            {
                smtp.Credentials = new NetworkCredential("jis202406@gmail.com", "crxalccjvnnzsfmz");
                smtp.EnableSsl = true;
                smtp.Send(message);
            }
        }
    }
}
