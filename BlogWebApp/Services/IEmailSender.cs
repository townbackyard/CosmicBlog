using System.Threading.Tasks;

namespace BlogWebApp.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string toAddress, string fromAddress, string subject, string plainTextBody, string? replyTo = null);
    }
}
