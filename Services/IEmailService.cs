using Microsoft.AspNetCore.Identity.UI.Services;

namespace LMS.Services;

public interface IEmailService : IEmailSender
{
    Task SendEmailToAdminAsync(string subject, string htmlMessage);
}
