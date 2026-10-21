using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace hrms.Shared.Services;

public class MailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "";
    public bool EnableSsl { get; set; } = true;
}

public class EmailService
{
    private readonly MailOptions _o;

    public EmailService(IOptions<MailOptions> options)
    {
        _o = options.Value;
    }

    public async Task SendAsync(string to, string subject, string html)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_o.FromAddress, _o.FromName),
            Subject = subject,
            Body = html,
            IsBodyHtml = true
        };
        msg.To.Add(to);

        using var client = new SmtpClient(_o.Host, _o.Port)
        {
            EnableSsl = _o.EnableSsl,
            Credentials = new NetworkCredential(_o.Username, _o.Password),
            Timeout = 15000
        };
        await client.SendMailAsync(msg);
    }
}