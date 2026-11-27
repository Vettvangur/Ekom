using Ekom.Brevo.Dispatching;
using Ekom.Brevo.Http;
using Ekom.Brevo.Mappers;
using Ekom.Brevo.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Brevo.Services;

internal sealed class BrevoContactsService(BrevoConfiguration configuration, BrevoMarketingDispatcher dispatcher, BrevoHttpClient client)
    : IBrevoContactsService
{
    public ValueTask<BrevoSubmission> UpsertAsync(BrevoContact contact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        if (Unavailable(contact.StoreAlias, cancellationToken) is { } unavailable) return ValueTask.FromResult(unavailable);
        return Submit(contact.StoreAlias, HttpMethod.Post, "contacts", BrevoContactMapper.Upsert(contact), true, cancellationToken);
    }

    public ValueTask<BrevoSubmission> SubscribeAsync(BrevoContact contact, long? listId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contact);
        if (Unavailable(contact.StoreAlias, cancellationToken) is { } unavailable) return ValueTask.FromResult(unavailable);
        var store = configuration.Store(contact.StoreAlias);
        var options = configuration.Options.Subscriptions;
        long? resolvedList = listId ?? store?.ListId ?? options.DefaultListId;
        if (resolvedList is null or <= 0)
            throw new InvalidOperationException("A positive Brevo subscription list ID is required.");
        if (options.DoubleOptIn)
        {
            var template = store?.DoubleOptInTemplateId ?? options.DoubleOptInTemplateId;
            var redirect = store?.DoubleOptInRedirectUrl ?? options.DoubleOptInRedirectUrl;
            if (template is null or <= 0 || redirect is not { IsAbsoluteUri: true }
                || (redirect.Scheme != Uri.UriSchemeHttps && redirect.Scheme != Uri.UriSchemeHttp))
                throw new InvalidOperationException("Double opt-in requires a template ID and an absolute HTTP(S) redirect URL.");
            return Submit(contact.StoreAlias, HttpMethod.Post, "contacts/doubleOptinConfirmation", new JsonObject
            {
                ["email"] = contact.Email,
                ["attributes"] = BrevoContactMapper.Attributes(contact),
                ["includeListIds"] = new JsonArray(JsonValue.Create(resolvedList.Value)),
                ["templateId"] = template.Value,
                ["redirectionUrl"] = redirect.AbsoluteUri,
            }, false, cancellationToken);
        }
        var payload = BrevoContactMapper.Upsert(contact);
        payload["listIds"] = new JsonArray(JsonValue.Create(resolvedList.Value));
        payload["emailBlacklisted"] = false;
        return Submit(contact.StoreAlias, HttpMethod.Post, "contacts", payload, true, cancellationToken);
    }

    public ValueTask<BrevoSubmission> UnsubscribeAsync(string storeAlias, string email, CancellationToken cancellationToken = default)
    {
        if (Unavailable(storeAlias, cancellationToken) is { } unavailable) return ValueTask.FromResult(unavailable);
        BrevoConfiguration.ValidateEmail(email);
        return Submit(storeAlias, HttpMethod.Put, "contacts/" + Uri.EscapeDataString(email),
            new JsonObject { ["emailBlacklisted"] = true }, true, cancellationToken);
    }

    public ValueTask<BrevoSubmission> RemoveFromListAsync(string storeAlias, string email, long listId, CancellationToken cancellationToken = default)
    {
        if (Unavailable(storeAlias, cancellationToken) is { } unavailable) return ValueTask.FromResult(unavailable);
        BrevoConfiguration.ValidateEmail(email);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listId);
        return Submit(storeAlias, HttpMethod.Put, "contacts/" + Uri.EscapeDataString(email),
            new JsonObject { ["unlinkListIds"] = new JsonArray(JsonValue.Create(listId)) }, true, cancellationToken);
    }

    public async Task<IReadOnlyList<BrevoList>> GetListsAsync(string storeAlias, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!configuration.Enabled(storeAlias) || !configuration.Options.Subscriptions.Enabled) return Array.Empty<BrevoList>();
        string? key = configuration.ApiKey(storeAlias);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Brevo API key is missing.");
        List<BrevoList> lists = [];
        for (int offset = 0; ; offset += 50)
        {
            string? response = await client.SendAsync(key, HttpMethod.Get, $"contacts/lists?limit=50&offset={offset}", null, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(response ?? throw new InvalidOperationException("Brevo returned no list data."));
            var page = doc.RootElement.GetProperty("lists");
            foreach (var item in page.EnumerateArray())
                lists.Add(new(item.GetProperty("id").GetInt64(), item.GetProperty("name").GetString() ?? ""));
            if (page.GetArrayLength() < 50) return lists;
        }
    }

    private BrevoSubmission? Unavailable(string alias, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        if (!configuration.Enabled(alias) || !configuration.Options.Subscriptions.Enabled)
            return new(BrevoSubmissionStatus.Disabled, Guid.NewGuid().ToString("N"));
        if (string.IsNullOrWhiteSpace(configuration.ApiKey(alias)))
            return new(BrevoSubmissionStatus.ConfigurationMissing, Guid.NewGuid().ToString("N"));
        return null;
    }

    private ValueTask<BrevoSubmission> Submit(string alias, HttpMethod method, string path, JsonObject payload, bool safeToReplay, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string id = Guid.NewGuid().ToString("N");
        if (!configuration.Enabled(alias) || !configuration.Options.Subscriptions.Enabled)
            return ValueTask.FromResult(new BrevoSubmission(BrevoSubmissionStatus.Disabled, id));
        string? key = configuration.ApiKey(alias);
        return ValueTask.FromResult(string.IsNullOrWhiteSpace(key)
            ? new(BrevoSubmissionStatus.ConfigurationMissing, id)
            : dispatcher.Enqueue(alias, key, method, path, payload.ToJsonString(), safeToReplay, id));
    }
}
