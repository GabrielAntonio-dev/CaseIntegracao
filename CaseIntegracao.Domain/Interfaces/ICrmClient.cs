using CaseIntegracao.Domain.Entities;
using CaseIntegracao.Domain.Enums;

namespace CaseIntegracao.Domain.Interfaces;

public interface ICrmClient
{
    Task GarantirClienteAsync(DadosCliente customer, CancellationToken cancellationToken = default);
    Task UpsertPedidoAsync(
        string orderId,
        StatusPedido status,
        decimal totalAmount,
        string currency,
        string customerExternalId,
        CancellationToken cancellationToken = default);
}
