using CaseIntegracao.Core.Domain.Entities;
using CaseIntegracao.Core.Domain.Interfaces;

namespace CaseIntegracao.Core.Infrastructure.Persistence;

public sealed class ProjecaoPedidoArquivoRepository : IProjecaoPedidoRepository
{
    private readonly ArquivoJsonStore<ProjecaoPedido> _store;

    public ProjecaoPedidoArquivoRepository(ArquivoJsonStore<ProjecaoPedido> store)
    {
        _store = store;
    }

    public async Task<ProjecaoPedido?> ObterPorIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var items = await _store.LerAsync(cancellationToken);
        return items.FirstOrDefault(x => string.Equals(x.OrderId, orderId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SalvarAsync(ProjecaoPedido projection, CancellationToken cancellationToken = default)
    {
        var items = await _store.LerAsync(cancellationToken);
        var index = items.FindIndex(x =>
            string.Equals(x.OrderId, projection.OrderId, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            items[index] = projection;
        }
        else
        {
            items.Add(projection);
        }

        await _store.EscreverAsync(items, cancellationToken);
    }
}
