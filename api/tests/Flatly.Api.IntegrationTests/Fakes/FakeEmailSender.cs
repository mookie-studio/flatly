using System.Collections.Concurrent;
using Flatly.Api.Modules.Platform.Email;

namespace Flatly.Api.IntegrationTests.Fakes;

/// <summary>
/// Replaces <see cref="IEmailSender"/> in the integration tests. Nothing is delivered: every message the API sends
/// is kept in <see cref="Outbox"/>, in the order it was sent, for the test to inspect.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _outbox = new();

    /// <summary>Snapshot of the messages sent so far, oldest first.</summary>
    public IReadOnlyList<EmailMessage> Outbox => _outbox.ToArray();

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        _outbox.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Empties the outbox, for a test that needs to ignore earlier messages.</summary>
    public void Clear()
    {
        while (_outbox.TryDequeue(out _))
        {
        }
    }
}
