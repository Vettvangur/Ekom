using Ekom.API;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace Ekom.Umb;

internal static class PriceSavingNotifications
{
    public static async Task ApplyAsync(ContentSavingNotification notification, CancellationToken ct)
    {
        if (!Configuration.Instance.UseDatabasePrices || notification.Cancel) return;
        try
        {
            var stores = API.Store.Instance.GetAllStores().ToList();
            var changes = new List<PriceMutation>();
            var fields = new List<(IContent Node, string Alias, string Value)>();
            var security = Configuration.Resolver.GetRequiredService<ISecurityService>();
            var access = Configuration.Resolver.GetRequiredService<IManagerAccessService>();
            foreach (var node in notification.SavedEntities)
            {
                foreach (var property in node.Properties.Where(property => property.PropertyType.PropertyEditorAlias == "Ekom.Price"))
                {
                    var raw = property.GetValue(null, null, false)?.ToString();
                    var rows = SavedPriceChanges.Create(node.Key, raw, stores, property.Alias);
                    if (rows.Count == 0) continue;
                    if (security.IsAuthenticated() && rows.Any(row => !access.CanAccessStore(row.StoreAlias)))
                        throw new InvalidOperationException("You do not have access to a store whose price was changed.");
                    changes.AddRange(rows);
                    fields.Add((node, property.Alias, SavedPriceChanges.Clear(raw!)));
                }
            }
            Prices.Instance.Validate(changes);
            await Prices.Instance.ApplyAsync(changes, ct).ConfigureAwait(false);
            // These values are persisted by the current content save. No extra save/publish is needed.
            foreach (var field in fields) field.Node.SetValue(field.Alias, field.Value);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            notification.CancelOperation(new EventMessage("Prices", ex.Message, EventMessageType.Error));
        }
    }
}
