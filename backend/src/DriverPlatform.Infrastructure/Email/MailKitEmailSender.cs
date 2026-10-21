using DriverPlatform.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DriverPlatform.Infrastructure.Email;

/// <summary>SMTP through MailKit (any provider: Brevo, Postmark, SES, Gmail Workspace…), or .eml files in development.</summary>
public sealed class MailKitEmailSender(IOptions<EmailOptions> options, IHostEnvironment environment) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? "", message.To));
        mime.Subject = message.Subject;

        var body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody };
        foreach (var attachment in message.Attachments)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }
        mime.Body = body.ToMessageBody();

        if (!string.IsNullOrWhiteSpace(o.PickupDirectory))
        {
            var folder = Path.GetFullPath(o.PickupDirectory, environment.ContentRootPath);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");
            await mime.WriteToAsync(file, cancellationToken);
            return;
        }

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(o.Host, o.Port, o.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrEmpty(o.Username))
        {
            await smtp.AuthenticateAsync(o.Username, o.Password, cancellationToken);
        }
        await smtp.SendAsync(mime, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
    }
}
