using System.ComponentModel.DataAnnotations;

namespace CaseIntegracao.Core.DTOs;

public sealed class RequisicaoWebhookPedido
{
    [Required]
    public required string EventId { get; init; }

    [Required]
    public required string EventType { get; init; }

    [Required]
    public required DateTimeOffset OccurredAt { get; init; }

    [Required]
    public required DadosWebhookPedido Data { get; init; }
}

public sealed class DadosWebhookPedido
{
    [Required]
    public required string OrderId { get; init; }

    /// <summary>
    /// Obrigatório para order.created / order.updated.
    /// Ignorado em order.canceled (status vira cancelado automaticamente).
    /// </summary>
    public string? Status { get; init; }

    [Range(0, double.MaxValue)]
    public required decimal TotalAmount { get; init; }

    [Required]
    public required string Currency { get; init; }

    [Required]
    public required DadosWebhookCliente Customer { get; init; }
}

public sealed class DadosWebhookCliente
{
    [Required]
    public required string ExternalId { get; init; }

    [Required]
    public required string Name { get; init; }

    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Document { get; init; }
}

public sealed class ResultadoProcessamentoEvento
{
    public required string EventId { get; init; }
    public required string Status { get; init; }
    public required bool AlreadyProcessed { get; init; }
    public string? Message { get; init; }
}

public sealed class RespostaEventoIntegracao
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string OrderId { get; init; }
    public required string Status { get; init; }
    public required int AttemptCount { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
}

public sealed class RespostaProjecaoPedido
{
    public required string OrderId { get; init; }
    public required string Status { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset LastOccurredAt { get; init; }
    public required DadosWebhookCliente Customer { get; init; }
}
