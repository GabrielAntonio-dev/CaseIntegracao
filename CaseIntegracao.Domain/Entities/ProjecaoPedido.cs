using CaseIntegracao.Domain.Enums;

namespace CaseIntegracao.Domain.Entities;

public sealed class ProjecaoPedido
{
    public required string OrderId { get; set; }
    public required StatusPedido Status { get; set; }
    public required decimal TotalAmount { get; set; }
    public required string Currency { get; set; }
    public required DadosCliente Customer { get; set; }
    public required DateTimeOffset LastOccurredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool EhMaisNovoOuIgual(DateTimeOffset occurredAt) =>
        occurredAt >= LastOccurredAt;

    public void Aplicar(
        StatusPedido status,
        decimal totalAmount,
        string currency,
        DadosCliente customer,
        DateTimeOffset occurredAt)
    {
        Status = status;
        TotalAmount = totalAmount;
        Currency = currency;
        Customer = customer;
        LastOccurredAt = occurredAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
