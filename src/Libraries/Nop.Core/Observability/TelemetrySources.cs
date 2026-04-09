using System.Diagnostics;

// Spans Customization:
namespace Nop.Core.Telemetry
{
    public static class TelemetrySources
    {
        public static readonly ActivitySource NopSource =
            new ActivitySource("NopCommerce.Custom");
    }
}