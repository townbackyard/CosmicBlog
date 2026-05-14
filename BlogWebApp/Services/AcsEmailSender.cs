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
        // EmailClient is created lazily on first SendAsync. The ctor must not throw
        // when ACS isn't configured -- ContactController depends on IEmailSender
        // via DI, and an eager-throw would 500 /contact on every visit in local
        // dev (where Contact.AcsConnectionString is empty by default). Failure
        // surfaces at send time as a user-facing "couldn't send" error instead.
        private readonly IOptions<AppSettings> _options;
        private EmailClient? _client;
        private readonly object _clientLock = new();

        public AcsEmailSender(IOptions<AppSettings> options)
        {
            _options = options;
        }

        private EmailClient GetClient()
        {
            if (_client != null) return _client;
            lock (_clientLock)
            {
                if (_client != null) return _client;
                var conn = _options.Value.Contact.AcsConnectionString;
                if (string.IsNullOrWhiteSpace(conn))
                    throw new System.InvalidOperationException("Contact.AcsConnectionString is not configured.");
                _client = new EmailClient(conn);
                return _client;
            }
        }

        public async Task SendAsync(string toAddress, string fromAddress, string subject, string plainTextBody, string? replyTo = null)
        {
            var client = GetClient();
            var content = new EmailContent(subject) { PlainText = plainTextBody };
            var recipients = new EmailRecipients(new[] { new EmailAddress(toAddress) });
            var message = new EmailMessage(fromAddress, recipients, content);
            if (!string.IsNullOrEmpty(replyTo))
                message.ReplyTo.Add(new EmailAddress(replyTo));

            // Fire-and-forget (WaitUntil.Started) -- the user gets immediate "Sent"
            // confirmation; the controller's try/catch handles ACS rejections.
            await client.SendAsync(WaitUntil.Started, message);
        }
    }
}
