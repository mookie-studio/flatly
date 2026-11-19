using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Flatly.Api.Modules.Platform.Email;

/// <summary>Sends email through the Resend API. This is the provider used in deployment.</summary>
public sealed class ResendEmailSender(HttpClient httpClient, IOptions<EmailOptions> options) : IEmailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        var endpoint = new Uri(settings.Resend.BaseUrl.TrimEnd('/') + "/emails");
        var payload = new ResendEmailRequest(
            From: $"{settings.FromName} <{settings.FromAddress}>",
            To: [message.To],
            Subject: message.Subject,
            Text: message.TextBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Resend.ApiKey ?? string.Empty);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // The response body is not included: it can echo the recipient address.
            throw new InvalidOperationException(
                $"Resend rejected the email with status {(int)response.StatusCode}.");
        }
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text);
}
