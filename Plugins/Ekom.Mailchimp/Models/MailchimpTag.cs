namespace Ekom.Mailchimp.Models;

public sealed record MailchimpTag
{
    public required long Id { get; init; }
    public required string Name { get; init; }
}
