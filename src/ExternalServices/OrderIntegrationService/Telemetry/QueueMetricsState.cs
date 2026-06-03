using System.Threading;

namespace OrderIntegrationService.Telemetry;

public sealed class QueueMetricsState
{
    private long _queueMessagesReady;
    private long _queueMessagesUnacked;
    private long _dlqMessagesReady;

    public QueueMetricsState(string queueName, string dlqName)
    {
        QueueName = queueName;
        DlqName = dlqName;
    }

    public string QueueName { get; }

    public string DlqName { get; }

    public long QueueMessagesReady => Interlocked.Read(ref _queueMessagesReady);

    public long QueueMessagesUnacked => Interlocked.Read(ref _queueMessagesUnacked);

    public long DlqMessagesReady => Interlocked.Read(ref _dlqMessagesReady);

    public void Update(long queueMessagesReady, long queueMessagesUnacked, long dlqMessagesReady)
    {
        Interlocked.Exchange(ref _queueMessagesReady, queueMessagesReady);
        Interlocked.Exchange(ref _queueMessagesUnacked, queueMessagesUnacked);
        Interlocked.Exchange(ref _dlqMessagesReady, dlqMessagesReady);
    }
}
