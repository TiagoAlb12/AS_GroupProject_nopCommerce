using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System;

namespace Nop.Core.Telemetry
{
    public sealed class TelemetryManager : IDisposable
    {
        private static readonly Lazy<TelemetryManager> instance =
            new Lazy<TelemetryManager>(() => new TelemetryManager());

        private readonly TracerProvider tracerProvider;
        private readonly MeterProvider meterProvider;
        private bool disposed = false;

        public static TelemetryManager Instance => instance.Value;

        private TelemetryManager()
        {
            Console.WriteLine(">>> TELEMETRY MANAGER CONSTRUCTOR START <<<");
            
            try
            {
                var resourceBuilder = ResourceBuilder.CreateDefault()
                    .AddService(
                        serviceName: "nopcommerce-service",
                        serviceVersion: "5.0.0",
                        serviceInstanceId: Environment.MachineName);

                Console.WriteLine(">>> CREATING TRACER PROVIDER <<<");
                
                tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.SetDbStatementForText = false;
                        options.RecordException = true;
                        options.EnableConnectionLevelAttributes = true;
                    })
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = (httpContext) =>
                        {
                            return !httpContext.Request.Path.StartsWithSegments("/health");
                        };
                    })
                    .AddSource("Nop.Web.CatalogController")
                    .AddSource("Nop.Services.Catalog.ProductService")
                    .AddSource("Nop.Services.Catalog.PriceCalculation")
                    .AddSource("Nop.Web.ProductModelFactory")
                    .AddSource("NopCommerce.Custom")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri("http://telemetry_service:4317");
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    })
                    .Build();
                
                Console.WriteLine(">>> TRACER PROVIDER CREATED <<<");

                Console.WriteLine(">>> CREATING METER PROVIDER <<<");
                
                meterProvider = Sdk.CreateMeterProviderBuilder()
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddMeter("NopCommerce.Custom")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri("http://telemetry_service:4317");
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    })
                    .Build();

                Console.WriteLine(">>> METER PROVIDER CREATED <<<");
                Console.WriteLine(">>> TELEMETRY MANAGER CONSTRUCTOR END <<<");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> ERROR IN TELEMETRY MANAGER: {ex.Message} <<<");
                Console.WriteLine($">>> STACK TRACE: {ex.StackTrace} <<<");
                throw;
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                tracerProvider?.Dispose();
                meterProvider?.ForceFlush(); 
                meterProvider?.Dispose();
                disposed = true;
            }
        }
    }
}