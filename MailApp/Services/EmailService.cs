using System.Collections.ObjectModel;
using MailApp.Model;
using MailKit;
using MailKit.Search;
using Newtonsoft.Json;
using FolderAccess = MailKit.FolderAccess;
using MessageSummaryItems = MailKit.MessageSummaryItems;

namespace MailApp.Services;

using MailKit.Net.Imap;
using MailKit.Security;

public class EmailService
{
    public async Task<bool> VerifyCredentialsAsync(string username, string password)
    {
        try
        {
            using (var client = new ImapClient())
            {
                await client.ConnectAsync("imap.wp.pl", 993, SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync(username, password);
                client.Disconnect(true);
                return true;
            }
        }
        catch (AuthenticationException ex)
        {
            await Shell.Current.DisplayAlert("Error", "Incorrect credentials", "OK");
            return false;
        }
        catch (ImapCommandException)
        {
            return false;
        }
        catch (ImapProtocolException)
        {
            return false;
        }
    }


    public bool IsOauthSupported(string email)
    {
        string emailProvider = email.Split('@')[1];
        return false;
    }
    
    public async Task<IList<IMailFolder>> GetFoldersAsync(string username, string password)
    {
        using (var client = new ImapClient())
        {
            await client.ConnectAsync("imap.wp.pl", 993, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(username, password);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);
            var folders = client.GetFolders(client.PersonalNamespaces[0]);
            await client.DisconnectAsync(true);
            return folders;
        }
    }
    
    public async Task<List<EmailEnvelope>> FetchAllEmailSummariesAsync(string username, string password, string folder)
    {
        List<EmailEnvelope> emailEnvelopes = new();
        
        using (var client = new ImapClient())
        {
            await client.ConnectAsync("imap.wp.pl", 993, SecureSocketOptions.SslOnConnect); 
            await client.AuthenticateAsync(username, password);
            var inbox = await client.GetFolderAsync(folder);
            await inbox.OpenAsync(FolderAccess.ReadOnly);
            var messages = await inbox.FetchAsync(0, -1, MessageSummaryItems.Fast | MessageSummaryItems.Envelope);
            foreach (var message in messages)
            {
                var single_email =  new EmailEnvelope()
                {
                    Subject = message.NormalizedSubject,
                    From =   message.Envelope.From.First().Name,
                    Date = message.Date.DateTime,
                    IsNotRead = !(message.Flags.Value.HasFlag(MessageFlags.Seen)),
                    Id = message.Index
                };
                emailEnvelopes.Add(single_email);
            };
            return emailEnvelopes;
        }
    }
    
    public async Task<MimeKit.MimeMessage> FetchEmailAsync(string username, string password, int id)
    {
        using (var client = new ImapClient())
        {
            await client.ConnectAsync("imap.wp.pl", 993, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(username, password);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);
            var message = await inbox.GetMessageAsync(id);
            await inbox.AddFlagsAsync(id, MessageFlags.Seen, true);
            await client.DisconnectAsync(true);
            return message;
        }
    }

    public async Task<List<EmailEnvelope>> SearchEmailsAsync(string username, string password, string searchQuery)
    {
        var emailEnvelopes = new List<EmailEnvelope>();

        using (var client = new ImapClient())
        {
            await client.ConnectAsync("imap.wp.pl", 993, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(username, password);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);
            // SearchQuery searchQuery = SearchQuery.SubjectContains(searchEmailQuery);

            var uidsSubjects = await inbox.SearchAsync(SearchQuery.SubjectContains(searchQuery));
            var uidsFrom = await inbox.SearchAsync(SearchQuery.FromContains(searchQuery));
            var uidsTo = await inbox.SearchAsync(SearchQuery.ToContains(searchQuery));
            var uidsBody = await inbox.SearchAsync(SearchQuery.BodyContains(searchQuery));
            var uids = uidsSubjects.Concat(uidsFrom).Concat(uidsTo).Concat(uidsBody).Distinct().ToList();
            var messages = await inbox.FetchAsync(uids, MessageSummaryItems.Fast | MessageSummaryItems.Envelope| MessageSummaryItems.UniqueId);

            foreach (var message in messages)
            {
                var singleEmail = new EmailEnvelope
                {
                    Subject = message.NormalizedSubject,
                    From = message.Envelope.From.First().Name,
                    Date = message.Date.DateTime,
                    IsNotRead = !(message.Flags.Value.HasFlag(MessageFlags.Seen)),
                    Id = message.Index
                };
                emailEnvelopes.Add(singleEmail);
            }

            await client.DisconnectAsync(true);
            return emailEnvelopes;
        }
    }

    public async Task<bool> DeleteEmailAsync(string username, string password, int id)
    {
        using (var client = new ImapClient())
        {
            await client.ConnectAsync("imap.wp.pl", 993, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(username, password);
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);
            await inbox.AddFlagsAsync(id, MessageFlags.Deleted, true);
            await inbox.ExpungeAsync();
            await client.DisconnectAsync(true);
            return true;
        }
    }

}