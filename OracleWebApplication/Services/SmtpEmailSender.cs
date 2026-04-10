using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OracleWebApplication.Models;

namespace OracleWebApplication.Services;

public class SmtpSettings
{
    public const string SectionName = "Smtp";
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "OCI Dashboard";
    public bool EnableSsl { get; set; } = true;
}

public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var subject = "Confirm your email – OCI Dashboard";
        var body = $"""
            <h2>Email Confirmation</h2>
            <p>Hi {user.DisplayName},</p>
            <p>Please confirm your email by clicking the link below:</p>
            <p><a href="{confirmationLink}">Confirm Email</a></p>
            <p>If you did not create an account, please ignore this email.</p>
            """;
        await SendAsync(email, subject, body);
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = "Reset your password – OCI Dashboard";
        var body = $"""
            <h2>Password Reset</h2>
            <p>Hi {user.DisplayName},</p>
            <p>We received a request to reset your password. Click the link below to set a new password:</p>
            <p><a href="{resetLink}">Reset Password</a></p>
            <p>This link will expire in 24 hours.</p>
            <p>If you did not request a password reset, please ignore this email.</p>
            """;
        await SendAsync(email, subject, body);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = "Password reset code – OCI Dashboard";
        var body = $"""
            <h2>Password Reset Code</h2>
            <p>Hi {user.DisplayName},</p>
            <p>Your password reset code is: <strong>{resetCode}</strong></p>
            <p>If you did not request this, please ignore this email.</p>
            """;
        await SendAsync(email, subject, body);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_settings.FromAddress, _settings.FromName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(_settings.Host, _settings.Port);
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            client.EnableSsl = _settings.EnableSsl;

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {Email} – subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }
}
