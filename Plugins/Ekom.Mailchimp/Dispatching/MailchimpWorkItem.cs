using Ekom.Mailchimp.Models;

namespace Ekom.Mailchimp.Dispatching;

internal abstract record MailchimpWorkItem(string StoreAlias, string Identifier);

internal sealed record SubscribeWorkItem(MailchimpSubscribeRequest Request)
    : MailchimpWorkItem(Request.StoreAlias, Request.Email);

internal sealed record UnsubscribeWorkItem(MailchimpUnsubscribeRequest Request)
    : MailchimpWorkItem(Request.StoreAlias, Request.Email);

internal sealed record PurchaseWorkItem(MailchimpPurchase Purchase)
    : MailchimpWorkItem(Purchase.StoreAlias, Purchase.OrderId);
