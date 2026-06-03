using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;

namespace OrderIntegrationService.Telemetry;

public sealed class RabbitMqQueueMetricsHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly QueueMetricsState _state;
    private readonly ILogger<RabbitMqQueueMetricsHostedService> _logger;

    public RabbitMqQueueMetricsHostedService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        QueueMetricsState state,
        ILogger<RabbitMqQueueMetricsHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshQueueMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to refresh RabbitMQ queue metrics");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RefreshQueueMetricsAsync(CancellationToken cancellationToken)
    {
        var queue = await GetQueueDetailsAsync(_state.QueueName, cancellationToken);
        var dlq = await GetQueueDetailsAsync(_state.DlqName, cancellationToken);

        _state.Update(
            queue?.MessagesReady ?? 0,
            queue?.MessagesUnacknowledged ?? 0,
            dlq?.MessagesReady ?? 0);
    }

    private async Task<RabbitMqQueueDetails?> GetQueueDetailsAsync(string queueName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildQueueUrl(queueName));
        request.Headers.Authorization = BuildAuthorizationHeader();

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RabbitMqQueueDetails>(cancellationToken);
    }

    private string BuildQueueUrl(string queueName)
    {
        var managementUrl = _configuration["RabbitMQ:ManagementUrl"] ?? "http://rabbitmq:15672";
        var virtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/";

        return $"{managementUrl.TrimEnd('/')}/api/queues/{Uri.EscapeDataString(virtualHost)}/{Uri.EscapeDataString(queueName)}";
    }

    private AuthenticationHeaderValue BuildAuthorizationHeader()
    {
        var username = _configuration["RabbitMQ:UserName"] ?? "guest";
        var password = _configuration["RabbitMQ:Password"] ?? "guest";
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));

        return new AuthenticationHeaderValue("Basic", token);
    }

    private sealed class RabbitMqQueueDetails
    {
        [JsonPropertyName("messages_ready")]
        public long MessagesReady { get; set; }

        [JsonPropertyName("messages_unacknowledged")]
        public long MessagesUnacknowledged { get; set; }
    }
}
