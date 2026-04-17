using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace TicketingSystem.Api.Services;

public interface IEmailService
{
    Task SendOtpAsync(string email, string otp);
    Task SendInvitationAsync(string email, string invitationLink, string userName);
    Task SendPasswordResetCodeAsync(string email, string resetCode, string userName);
    Task SendEmailAsync(string email, string subject, string body);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// Generic email sender for ticket notifications
    public async Task SendEmailAsync(string email, string subject, string body)
    {
        try
        {
            if (string.IsNullOrEmpty(email))
                throw new InvalidOperationException("Email is required");

            if (string.IsNullOrEmpty(subject))
                throw new InvalidOperationException("Subject is required");

            if (string.IsNullOrEmpty(body))
                throw new InvalidOperationException("Body is required");

            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderPassword = _configuration["Email:SenderPassword"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                throw new InvalidOperationException("Email configuration is missing");

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                client.Timeout = 10000;

                using (var mailMessage = new MailMessage(senderEmail, email)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    await client.SendMailAsync(mailMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email send failed for {email}: {ex.Message}");
        }
    }

    /// Send OTP code to user email
    public async Task SendOtpAsync(string email, string otp)
    {
        try
        {
            if (string.IsNullOrEmpty(email))
                throw new InvalidOperationException("Email is required");

            if (string.IsNullOrEmpty(otp))
                throw new InvalidOperationException("OTP is required");

            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderPassword = _configuration["Email:SenderPassword"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                throw new InvalidOperationException("Email configuration is missing");

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                client.Timeout = 10000;

                var subject = "Your OTP Code - Ticketing System";
                var body = $@"
                    <h2>Email Verification</h2>
                    <p>Your OTP code is:</p>
                    <h1 style='color: #007bff;'>{otp}</h1>
                    <p>This code expires in 10 minutes.</p>
                    <p>Do not share this code with anyone.</p>
                ";

                using (var mailMessage = new MailMessage(senderEmail, email)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    await client.SendMailAsync(mailMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email send failed for {email}: {ex.Message}");
        }
    }

    /// Send invitation link to invited users
    public async Task SendInvitationAsync(string email, string invitationLink, string userName)
    {
        try
        {
            if (string.IsNullOrEmpty(email))
                throw new InvalidOperationException("Email is required");

            if (string.IsNullOrEmpty(invitationLink))
                throw new InvalidOperationException("Invitation link is required");

            if (string.IsNullOrEmpty(userName))
                throw new InvalidOperationException("User name is required");

            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderPassword = _configuration["Email:SenderPassword"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                throw new InvalidOperationException("Email configuration is missing");

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                client.Timeout = 10000;

                var subject = "You're Invited to Ticketing System";
                var body = $@"
                    <h2>Welcome, {userName}!</h2>
                    <p>You've been invited to join the Ticketing System.</p>
                    <p><a href='{invitationLink}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Accept Invitation</a></p>
                    <p>This link expires in 7 days.</p>
                ";

                using (var mailMessage = new MailMessage(senderEmail, email)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    await client.SendMailAsync(mailMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email send failed for {email}: {ex.Message}");
        }
    }

    /// Send password reset code to user email
    public async Task SendPasswordResetCodeAsync(string email, string resetCode, string userName)
    {
        try
        {
            if (string.IsNullOrEmpty(email))
                throw new InvalidOperationException("Email is required");

            if (string.IsNullOrEmpty(resetCode))
                throw new InvalidOperationException("Reset code is required");

            if (string.IsNullOrEmpty(userName))
                throw new InvalidOperationException("User name is required");

            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderPassword = _configuration["Email:SenderPassword"];

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                throw new InvalidOperationException("Email configuration is missing");

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                client.Timeout = 10000;

                var subject = "Password Reset Code - Ticketing System";
                var body = $@"
                    <h2>Password Reset Request</h2>
                    <p>Hi {userName},</p>
                    <p>We received a request to reset your password.</p>
                    <p>Your password reset code is:</p>
                    <h1 style='color: #007bff;'>{resetCode}</h1>
                    <p>This code expires in 10 minutes.</p>
                    <p>If you didn't request this, please ignore this email.</p>
                    <p>Do not share this code with anyone.</p>
                ";

                using (var mailMessage = new MailMessage(senderEmail, email)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    await client.SendMailAsync(mailMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Email send failed for {email}: {ex.Message}");
        }
    }
}