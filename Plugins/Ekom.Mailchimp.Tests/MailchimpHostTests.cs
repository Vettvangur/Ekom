using Ekom.Mailchimp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp.Tests;

public sealed class MailchimpHostTests
{
    [Fact]
    public void AddMailchimp_RegistersPublicService()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddMailchimp())
            .Build();
        using IServiceScope scope = host.Services.CreateScope();

        IMailchimpService service = scope.ServiceProvider.GetRequiredService<IMailchimpService>();

        Assert.NotNull(service);
    }

    [Fact]
    public async Task Host_StartsWithMissingOperationalConfiguration()
    {
        var logger = new RecordingLogger<MailchimpConfigurationResolver>();
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMailchimp(options => options.Enabled = true);
                services.AddSingleton<ILogger<MailchimpConfigurationResolver>>(logger);
            })
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(4, logger.Errors.Count);
    }

    [Fact]
    public async Task Host_RejectsInvalidDispatcherConfiguration()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddMailchimp(options =>
            {
                options.Dispatching.MaxQueueSize = 0;
            }))
            .Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Host_StartsWithStoreApiKeyAndGlobalIds()
    {
        var logger = new RecordingLogger<MailchimpConfigurationResolver>();
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddMailchimp(options =>
                {
                    options.Enabled = true;
                    options.AudienceId = "global-audience";
                    options.EcommerceStoreId = "global-store";
                    options.Stores.Add(new MailchimpStoreOptions
                    {
                        Alias = "HVerslun",
                        ApiKey = "store-key-us21",
                    });
                });
                services.AddSingleton<ILogger<MailchimpConfigurationResolver>>(logger);
            })
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Empty(logger.Errors);
    }
}
