namespace CaseIntegracao.Core.Interfaces;

public interface IMetricasCollector
{
    void IncrementarRecebidos();
    void IncrementarSincronizados();
    void IncrementarFalhas();
    void IncrementarRetries();
    void IncrementarIgnoradosObsoletos();
    void IncrementarIdempotentes();
    MetricasIntegracao Snapshot();
}

public sealed record MetricasIntegracao(
    long Received,
    long Synced,
    long Failed,
    long Retries,
    long SkippedStale,
    long IdempotentHits);
