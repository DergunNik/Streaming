using System.Net.Mail;
using Email;
using EmailService.Settings;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace EmailService.Service;

[Authorize(Roles = "Admin,InnerService")]
public class EmailService(
    EmailCredentials credentials,
    ILogger<EmailService> logger) : Email.EmailService.EmailServiceBase
{
    public override async Task<Empty> SendEmail(EmailRequest request, ServerCallContext context)
    {
        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(credentials.Email, credentials.AppPassword);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(request.From, credentials.Email));
            message.Subject = request.Subject;
            message.Body = new TextPart("plain") { Text = request.Body };

            foreach (var recepient in request.To)
            {
                if (!MailAddress.TryCreate(recepient, out _))
                {
                    logger.LogWarning("Could not parse email address: {address}", recepient);
                    continue;
                }

                message.To.Add(MailboxAddress.Parse(recepient));
            }

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