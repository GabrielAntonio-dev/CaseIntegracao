using CaseIntegracao.Core.Domain.Entities;

namespace CaseIntegracao.Core.Domain.Interfaces;

public interface IProjecaoPedidoRepository
{
    Task<ProjecaoPedido?> ObterPorIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task SalvarAsync(ProjecaoPedido projection, CancellationToken cancellationToken = default);
}
