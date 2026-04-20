using Ekom.Algolia;
using Ekom.Klaviyo;
using Ekom.Klaviyo.Enrichers.OrderEnricher;
using Ekom.Klaviyo.Enrichers.TrackingEnricher;
using Ekom.Services;
using Newtonsoft.Json.Serialization;

namespace Ekom.Site;

public class Startup
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup" /> class.
    /// </summary>
    /// <param name="webHostEnvironment">The web hosting environment.</param>
    /// <param name="config">The configuration.</param>
    /// <remarks>
    /// Only a few services are possible to be injected here https://github.com/dotnet/aspnetcore/issues/9337.
    /// </remarks>
    public Startup(IWebHostEnvironment webHostEnvironment, IConfiguration config)
    {
        _env = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Configures the services.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <remarks>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940.
    /// </remarks>
    public void ConfigureServices(IServiceCollection services)
    {            
        services
            // https://docs.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-5.0#compression-with-secure-protocol
            .AddResponseCompression(options => options.EnableForHttps = true)
            // Make sure you call this previous to AddMvc
            .AddCors()
            .AddControllers()
            .AddNewtonsoftJson(opts =>
            {
                opts.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                opts.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            });
        

        services.AddUmbraco(_env, _config)
            .AddBackOffice()
            .AddWebsite()
            .AddComposers()
            //.AddAzureBlobMediaFileSystem()
            .AddMembersIdentity()
            //.AddCdnMediaUrlProvider() // (optional) add the CDN Media UrlProvider
            .Build();


        //services.AddApplicationInsightsTelemetry(_config["ApplicationInsights:ConnectionString"]);
        
        services.AddScoped<UmbracoService>();
        services.AddTransient<IProductFilterService, CustomProductFilterService>();
        services.AddTransient<IOrderManagerActionProvider, DemoOrderManagerActionProvider>();
        //services.AddTransient<Service>();
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
        });


        services.AddSingleton<IKlaviyoPlacedOrderEnricher, CustomPlacedOrderEnriching>();
        services.AddSingleton<IKlaviyoTrackingEnricher, CustomTrackingEnricher>();

        services.AddKlaviyo();
        services.AddAlgolia();
        //services.AddEkomValitorPay(_config);
    }

    /// <summary>
    /// Configures the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="env">The web hosting environment.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor, IConfiguration config)
    {
        GlobalSettings.HttpContextAccessor = httpContextAccessor;

        app.UseRequestLocalization();

        if (env.IsProduction())
        {
            app.UseResponseCompression();
        }

        if (!env.IsProduction())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseUmbraco()
            .WithMiddleware(u =>
            {
                u.UseBackOffice();
                u.UseWebsite();

                app.UseCors(builder => builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
                

            })
            .WithEndpoints(u =>
            {
                u.UseInstallerEndpoints();
                u.UseBackOfficeEndpoints();
                u.UseWebsiteEndpoints();
            });
    }
}
