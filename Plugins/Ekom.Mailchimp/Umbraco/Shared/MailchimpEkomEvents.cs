using Ekom.Events;
using Ekom.Mailchimp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Mailchimp.Events;

#if UMBRACO_18
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by the Umbraco component collection.")]
internal sealed class MailchimpEkomEvents : IAsyncComponent
#else
internal sealed class MailchimpEkomEvents : IComponent
#endif
{
    private readonly MailchimpOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public MailchimpEkomEvents(IOptions<MailchimpOptions> options, IServiceScopeFactory scopeFactory)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
    }

#if UMBRACO_18
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
#else
    public void Initialize()
#endif
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
#if UMBRACO_18
        return Task.CompletedTask;
#endif
    }

    private async Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Purchases.Enabled || !_options.Purchases.TrackCompletedCheckouts)
        {
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        IMailchimpService service = scope.ServiceProvider.GetRequiredService<IMailchimpService>();
        await service.TrackPurchaseAsync(args.OrderInfo, ct).ConfigureAwait(false);
    }

#if UMBRACO_18
    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
#else
    public void Terminate()
#endif
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
#if UMBRACO_18
        return Task.CompletedTask;
#endif
    }
}

public sealed class MailchimpComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<MailchimpEkomEvents>();
    }
}
