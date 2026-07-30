using CaseIntegracao.Core.Domain.Interfaces;

namespace CaseIntegracao.Core.Infrastructure.Metrics;

public sealed class MetricasEmMemoriaCollector : IMetricasCollector
{
    private long _received;
    private long _synced;
    private long _failed;
    private long _retries;
    private long _skippedStale;
    private long _idempotentHits;

    public void IncrementarRecebidos() => Interlocked.Increment(ref _received);
    public void IncrementarSincronizados() => Interlocked.Increment(ref _synced);
    public void IncrementarFalhas() => Interlocked.Increment(ref _failed);
    public void IncrementarRetries() => Interlocked.Increment(ref _retries);
    public void IncrementarIgnoradosObsoletos() => Interlocked.Increment(ref _skippedStale);
    public void IncrementarIdempotentes() => Interlocked.Increment(ref _idempotentHits);

    public MetricasIntegracao Snapshot() =>
        new(
            Interlocked.Read(ref _received),
            Interlocked.Read(ref _synced),
            Interlocked.Read(ref _failed),
            Interlocked.Read(ref _retries),
            Interlocked.Read(ref _skippedStale),
            Interlocked.Read(ref _idempotentHits));
}
