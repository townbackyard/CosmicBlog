using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using BlogWebApp;

namespace BlogWebApp.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string toAddress, string fromAddress, string subject, string plainTextBody, string? replyTo = null);
    }

    public class AcsEmailSender : IEmailSender
    {
        private readonly EmailClient _client;

        public AcsEmailSender(IOptions<AppSettings> options)
        {
            var conn = options.Value.Contact.AcsConnectionString;
            if (string.IsNullOrWhiteSpace(conn))
                throw new System.InvalidOperationException("Contact.AcsConnectionString is not configured.");
            _client = new EmailClient(conn);
        }

        public async Task SendAsync(string toAddress, string fromAddress, string subject, string plainTextBody, string? replyTo = null)
        {
            var content = new EmailContent(subject) { PlainText = plainTextBody };
            var recipients = new EmailRecipients(new[] { new EmailAddress(toAddress) });
            var message = new EmailMessage(fromAddress, recipients, content);
            if (!string.IsNullOrEmpty(replyTo))
                message.ReplyTo.Add(new EmailAddress(replyTo));

            // Fire-and-forget (WaitUntil.Started) -- the user gets immediate "Sent"
            // confirmation; the controller's try/catch handles ACS rejections.
            await _client.SendAsync(WaitUntil.Started, message);
        }
    }
}
