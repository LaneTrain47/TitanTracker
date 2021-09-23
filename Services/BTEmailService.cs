using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TitanTracker.Models;

namespace Titan_BugTracker.Services
{
    public class BTEmailService : IEmailSender
    {
        //private readonly MailSettings _mailSettings;
        private readonly IConfiguration _configuration;

        public BTEmailService(IConfiguration configuration)
        {
            //_mailSettings = mailSettings.Value;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string emailTo, string subject, string htmlMessage)
        {
            MimeMessage email = new();

            email.Sender = MailboxAddress.Parse(_configuration["MailSettings:Email"]);
            email.To.Add(MailboxAddress.Parse(emailTo));
            email.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };

            email.Body = builder.ToMessageBody();

            try
            {
                using var smtp = new SmtpClient();
                smtp.Connect(_configuration["MailSettings:Host"], Convert.ToInt32(_configuration["MailSettings:Port"]), SecureSocketOptions.StartTls);
                smtp.Authenticate(_configuration["MailSettings:Email"], _configuration["MailSettings:Password"]);

                await smtp.SendAsync(email);

                smtp.Disconnect(true);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}