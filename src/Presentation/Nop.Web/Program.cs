using Autofac.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Infrastructure.Extensions;
using Nop.Core.Telemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;

namespace Nop.Web;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile(NopConfigurationDefaults.AppSettingsFilePath, true, true);
        if (!string.IsNullOrEmpty(builder.Environment?.EnvironmentName))
        {
            var path = string.Format(NopConfigurationDefaults.AppSettingsEnvironmentFilePath, builder.Environment.EnvironmentName);
            builder.Configuration.AddJsonFile(path, true, true);
        }
        builder.Configuration.AddEnvironmentVariables();

        // Logging com OpenTelemetry
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("nopcommerce-service"));

            // Exporta para o OTLP (OpenTelemetry Protocol)
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri("http://telemetry_service:4318/v1/logs");
                otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            });

            // Exporta para o Console
            options.AddConsoleExporter();

            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });

        // Métricas com OpenTelemetry
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddMeter("NopCommerce.Custom")
                .AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri("http://telemetry_service:4317");
                    otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                })
                .AddConsoleExporter());

        //load application settings
        builder.Services.ConfigureApplicationSettings(builder);

        var appSettings = Singleton<AppSettings>.Instance;
        var useAutofac = appSettings.Get<CommonConfig>().UseAutofac;

        if (useAutofac)
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        else
        {
            builder.Host.UseDefaultServiceProvider(options =>
            {
                //we don't validate the scopes, since at the app start and the initial configuration we need 
                //to resolve some services (registered as "scoped") through the root container
                options.ValidateScopes = false;
                options.ValidateOnBuild = true;
            });
        }

        //add services to the application and configure service provider
        builder.Services.ConfigureApplicationServices(builder);

        var telemetry = TelemetryManager.Instance;

        var app = builder.Build();

        app.Lifetime.ApplicationStopping.Register(() => telemetry.Dispose());

        app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode == 404)
            {
                // Captura URL inválida
                TelemetryMetrics.SearchErrors.Add(1,
                    new KeyValuePair<string, object?>("error_type", "NotFound"),
                    new KeyValuePair<string, object?>("path", context.Request.Path),
                    new KeyValuePair<string, object?>("method", context.Request.Method));

                // Log opcional
                var logger = context.RequestServices.GetService<ILogger<Program>>();
                logger?.LogWarning("404 Not Found: {Method} {Path}", 
                    context.Request.Method, context.Request.Path);
            }
        });

        //configure the application HTTP request pipeline
        app.ConfigureRequestPipeline();
        await app.PublishAppStartedEventAsync();

        await app.RunAsync();
    }
}