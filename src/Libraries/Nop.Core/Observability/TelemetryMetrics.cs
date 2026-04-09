using System.Diagnostics.Metrics;

namespace Nop.Core.Telemetry
{
    public static class TelemetryMetrics
    {
        public static readonly Meter Meter = new("NopCommerce.Custom");

        public static readonly Counter<int> SearchRequests =
            Meter.CreateCounter<int>(
                "nopcommerce_search_requests_total",
                description: "Total number of product search requests");

        public static readonly Histogram<double> SearchDurationMs =
            Meter.CreateHistogram<double>(
                "nopcommerce_search_duration_ms",
                unit: "ms",
                description: "Duration of product search operations");

        public static readonly Histogram<int> SearchResultsCount =
            Meter.CreateHistogram<int>(
                "nopcommerce_search_results_count",
                description: "Number of products returned by search");

        public static readonly Histogram<double> ProductDetailsLoadDurationMs =
            Meter.CreateHistogram<double>(
                "nopcommerce_product_details_load_duration_ms",
                unit: "ms",
                description: "Duration of product details loading");

        public static readonly Histogram<double> ProductPriceCalculationDurationMs =
            Meter.CreateHistogram<double>(
                "nopcommerce_price_calculation_duration_ms",
                unit: "ms",
                description: "Duration of product price calculation");

        public static readonly Counter<int> SearchErrors =
            Meter.CreateCounter<int>("nopcommerce_search_errors_total",
                description: "Total number of search errors");
    }
}