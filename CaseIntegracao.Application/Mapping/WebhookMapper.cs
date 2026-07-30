using CaseIntegracao.Application.DTOs;
using CaseIntegracao.Domain.Entities;
using CaseIntegracao.Domain.Enums;

namespace CaseIntegracao.Application.Mapping;

public static class WebhookMapper
{
    public static EventoIntegracao ParaDominio(RequisicaoWebhookPedido request)
    {
        var eventType = AnalisarTipoEvento(request.EventType);

        return new EventoIntegracao
        {
            EventId = request.EventId.Trim(),
            EventType = eventType,
            OccurredAt = request.OccurredAt.ToUniversalTime(),
            OrderId = request.Data.OrderId.Trim(),
            OrderStatus = ResolverStatusPedido(eventType, request.Data.Status),
            TotalAmount = request.Data.TotalAmount,
            Currency = request.Data.Currency.Trim().ToUpperInvariant(),
            Customer = new DadosCliente
            {
                ExternalId = request.Data.Customer.ExternalId.Trim(),
                Name = request.Data.Customer.Name.Trim(),
                Email = request.Data.Customer.Email.Trim(),
                Document = request.Data.Customer.Document.Trim()
            }
        };
    }

    /// <summary>
    /// order.canceled implica status cancelado independentemente de data.status.
    /// </summary>
    public static StatusPedido ResolverStatusPedido(TipoEventoPedido eventType, string? status) =>
        eventType == TipoEventoPedido.PedidoCancelado
            ? StatusPedido.Cancelado
            : AnalisarStatusPedido(status);

    public static RespostaEventoIntegracao ParaResposta(EventoIntegracao entity) =>
        new()
        {
            EventId = entity.EventId,
            EventType = ParaTextoTipoEvento(entity.EventType),
            OccurredAt = entity.OccurredAt,
            OrderId = entity.OrderId,
            Status = entity.Status.ToString(),
            AttemptCount = entity.AttemptCount,
            NextRetryAt = entity.NextRetryAt,
            LastError = entity.LastError,
            ReceivedAt = entity.ReceivedAt,
            ProcessedAt = entity.ProcessedAt
        };

    public static RespostaProjecaoPedido ParaResposta(ProjecaoPedido projection) =>
        new()
        {
            OrderId = projection.OrderId,
            Status = projection.Status.ToString().ToLowerInvariant(),
            TotalAmount = projection.TotalAmount,
            Currency = projection.Currency,
            LastOccurredAt = projection.LastOccurredAt,
            Customer = new DadosWebhookCliente
            {
                ExternalId = projection.Customer.ExternalId,
                Name = projection.Customer.Name,
                Email = projection.Customer.Email,
                Document = projection.Customer.Document
            }
        };

    public static TipoEventoPedido AnalisarTipoEvento(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "order.created" => TipoEventoPedido.PedidoCriado,
            "order.updated" => TipoEventoPedido.PedidoAtualizado,
            "order.canceled" or "order.cancelled" => TipoEventoPedido.PedidoCancelado,
            _ => throw new ArgumentException($"eventType não suportado '{value}'.")
        };

    public static StatusPedido AnalisarStatusPedido(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("data.status é obrigatório para order.created e order.updated.");
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "pending" => StatusPedido.Pendente,
            "confirmed" => StatusPedido.Confirmado,
            "canceled" or "cancelled" => StatusPedido.Cancelado,
            _ => throw new ArgumentException($"status de pedido não suportado '{value}'.")
        };
    }

    public static string ParaTextoTipoEvento(TipoEventoPedido eventType) =>
        eventType switch
        {
            TipoEventoPedido.PedidoCriado => "order.created",
            TipoEventoPedido.PedidoAtualizado => "order.updated",
            TipoEventoPedido.PedidoCancelado => "order.canceled",
            _ => eventType.ToString()
        };
}
