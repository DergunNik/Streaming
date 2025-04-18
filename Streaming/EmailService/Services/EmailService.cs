using System.Net.Mail;
using Email;
using EmailService.Settings;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MailKit.Security;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace EmailService.Services;

public class EmailService(
    EmailCredentials credentials,
    ILogger<EmailService> logger) : Email.EmailService.EmailServiceBase
{
    public override async Task<Empty> SendEmail(EmailRequest request, ServerCallContext context)
    {
        if (!MailAddress.TryCreate(request.To, out var _))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid email address."));
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(request.From, credentials.Email));
        message.To.Add(MailboxAddress.Parse(request.To));
        message.Subject = request.Subject;
        message.Body = new TextPart("plain") { Text = request.Body };

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(credentials.Email, credentials.AppPassword);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
            return new Empty();
        }
        catch (Exception ex)
        {
            logger.LogCritical("Can't send email: {ex}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, ""));
        }
    }
}
