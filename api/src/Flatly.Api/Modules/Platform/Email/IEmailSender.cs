namespace Flatly.Api.Modules.Platform.Email;

/// <summary>
/// Sends email on behalf of the application. Notifications depend on this interface, never on a provider SDK,
/// so tests replace it with a fake that keeps the messages in an outbox.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends one message. Throws when the message cannot be handed to the delivery provider.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>A plain-text email to one recipient.</summary>
/// <param name="To">Recipient address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="TextBody">Plain-text body.</param>
public sealed record EmailMessage(string To, string Subject, string TextBody);
