using CaseIntegracao.Core.Enums;

namespace CaseIntegracao.Core.Entities;

public sealed class EventoIntegracao
{
    public required string EventId { get; init; }
    public required TipoEventoPedido EventType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string OrderId { get; init; }
    public required StatusPedido OrderStatus { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required DadosCliente Customer { get; init; }
    public StatusProcessamentoEvento Status { get; set; } = StatusProcessamentoEvento.Recebido;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }

    public bool EhSucessoTerminal =>
        Status is StatusProcessamentoEvento.Sincronizado or StatusProcessamentoEvento.IgnoradoObsoleto;

    public bool PodeRetentar(int maxAttempts) =>
        Status == StatusProcessamentoEvento.Falhou &&
        AttemptCount < maxAttempts &&
        NextRetryAt.HasValue &&
        NextRetryAt.Value <= DateTimeOffset.UtcNow;

    public void MarcarProcessando()
    {
        Status = StatusProcessamentoEvento.Processando;
        AttemptCount++;
        LastError = null;
    }

    public void MarcarSincronizado()
    {
        Status = StatusProcessamentoEvento.Sincronizado;
        NextRetryAt = null;
        LastError = null;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void MarcarIgnoradoObsoleto()
    {
        Status = StatusProcessamentoEvento.IgnoradoObsoleto;
        NextRetryAt = null;
        LastError = null;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void MarcarFalhou(string error, DateTimeOffset nextRetryAt)
    {
        Status = StatusProcessamentoEvento.Falhou;
        LastError = error;
        NextRetryAt = nextRetryAt;
    }

    public void MarcarCartaMortua(string error)
    {
        Status = StatusProcessamentoEvento.CartaMortua;
        LastError = error;
        NextRetryAt = null;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
