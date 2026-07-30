using CaseIntegracao.Core.Entities;

namespace CaseIntegracao.Core.Interfaces;

public interface IProjecaoPedidoRepository
{
    Task<ProjecaoPedido?> ObterPorIdAsync(string orderId, CancellationToken cancellationToken = default);
    Task SalvarAsync(ProjecaoPedido projection, CancellationToken cancellationToken = default);
}
