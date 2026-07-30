using CaseIntegracao.Core.Domain.Entities;
using CaseIntegracao.Core.Domain.Enums;

namespace CaseIntegracao.Core.Domain.Interfaces;

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
