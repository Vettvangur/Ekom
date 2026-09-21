using Ekom.Mailchimp.Exceptions;
using Ekom.Mailchimp.Mappers;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ekom.Mailchimp.Clients;

internal interface IMailchimpTransactionalClient
{
    Task<IReadOnlyList<MailchimpTransactionalSendResult>> SendAsync(
        MailchimpTransactionalMessage message,
        CancellationToken ct);

    Task<IReadOnlyList<MailchimpTransactionalSendResult>> SendTemplateAsync(
        MailchimpTransactionalTemplateMessage message,
        CancellationToken ct);
}

internal sealed record MailchimpTransactionalSendResult(
    string Email,
    string Status,
    string? RejectReason,
    string? Id);

internal sealed class MailchimpTransactionalClient : IMailchimpTransactionalClient
{
    private readonly IMailchimpTransactionalConfigurationResolver _configurationResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MailchimpTransactionalClient> _logger;

    public MailchimpTransactionalClient(
        IMailchimpTransactionalConfigurationResolver configurationResolver,
        IHttpClientFactory httpClientFactory,
        ILogger<MailchimpTransactionalClient> logger)
    {
        _configurationResolver = configurationResolver;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<IReadOnlyList<MailchimpTransactionalSendResult>> SendAsync(
        MailchimpTransactionalMessage message,
        CancellationToken ct)
        => SendCoreAsync(
            message.StoreAlias,
            "messages/send.json",
            configuration => MailchimpTransactionalPayloadMapper.CreatePayload(message, configuration),
            ct);

    public Task<IReadOnlyList<MailchimpTransactionalSendResult>> SendTemplateAsync(
        MailchimpTransactionalTemplateMessage message,
        CancellationToken ct)
        => SendCoreAsync(
            message.StoreAlias,
            "messages/send-template.json",
            configuration => MailchimpTransactionalPayloadMapper.CreatePayload(message, configuration),
            ct);

    private async Task<IReadOnlyList<MailchimpTransactionalSendResult>> SendCoreAsync(
        string storeAlias,
        string path,
        Func<MailchimpTransactionalStoreConfiguration, object> createPayload,
        CancellationToken ct)
    {
        if (!_configurationResolver.TryResolve(storeAlias, out MailchimpTransactionalStoreConfiguration? configuration))
        {
            throw new InvalidOperationException(
                $"Mailchimp Transactional configuration is incomplete for store '{storeAlias}'.");
        }

        using HttpClient httpClient = _httpClientFactory.CreateClient("Ekom.Mailchimp.Transactional");
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(
                createPayload(configuration),
                options: MailchimpTransactionalPayloadMapper.JsonOptions),
        };
        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            (string? errorName, string? errorMessage) = ReadError(responseBody);
            _logger.LogWarning(
                "Mailchimp Transactional request to {Path} failed with status {StatusCode}",
                path,
                (int)response.StatusCode);
            throw new MailchimpTransactionalApiException(
                response.StatusCode,
                path,
                errorName,
                errorMessage,
                GetRetryAfter(response));
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            (string? errorName, string? errorMessage) = ReadError(document.RootElement);
            throw new MailchimpTransactionalApiException(
                response.StatusCode,
                path,
                errorName,
                errorMessage);
        }
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Mailchimp Transactional response must be an array.");
        }

        var results = new List<MailchimpTransactionalSendResult>();
        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            results.Add(new MailchimpTransactionalSendResult(
                GetRequiredString(item, "email"),
                GetRequiredString(item, "status"),
                GetOptionalString(item, "reject_reason"),
                GetOptionalString(item, "_id")));
        }
        return results;
    }

    private static (string? Name, string? Message) ReadError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return (null, null);
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            return ReadError(document.RootElement);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static (string? Name, string? Message) ReadError(JsonElement element)
        => (GetOptionalString(element, "name"), GetOptionalString(element, "message"));

    private static string GetRequiredString(JsonElement element, string propertyName)
        => GetOptionalString(element, propertyName)
            ?? throw new JsonException($"Mailchimp Transactional response is missing '{propertyName}'.");

    private static string? GetOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }
        if (response.Headers.RetryAfter?.Date is not { } date)
        {
            return null;
        }

        TimeSpan retryAfter = date - DateTimeOffset.UtcNow;
        return retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter;
    }
}
