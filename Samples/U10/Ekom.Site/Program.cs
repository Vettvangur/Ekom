namespace Ekom.Site;

public static class Program
{
    public static void Main(string[] args)
        => CreateHostBuilder(args)
            .Build()
            .Run();

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment.EnvironmentName;

                // Load base configuration
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                // Load environment-specific configuration
                config.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);

                if (context.HostingEnvironment.IsDevelopment())
                {
                    config.AddUserSecrets<Startup>();
                }
                else
                {
                    config.AddEnvironmentVariables();
                }
            })
            .ConfigureUmbracoDefaults()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStaticWebAssets();
                webBuilder.UseStartup<Startup>();
            });
}
