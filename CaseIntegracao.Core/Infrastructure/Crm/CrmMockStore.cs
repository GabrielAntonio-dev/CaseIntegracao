using CaseIntegracao.Core.Infrastructure.Persistence;

namespace CaseIntegracao.Core.Infrastructure.Crm;

public sealed class CrmMockStore
{
    private readonly ArquivoJsonStore<CrmCustomerResponse> _customers;
    private readonly ArquivoJsonStore<CrmOrderResponse> _orders;

    public CrmMockStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _customers = new ArquivoJsonStore<CrmCustomerResponse>(Path.Combine(dataDirectory, "customers.json"));
        _orders = new ArquivoJsonStore<CrmOrderResponse>(Path.Combine(dataDirectory, "orders.json"));
    }

    public async Task<CrmCustomerResponse?> ObterClienteAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var items = await _customers.LerAsync(cancellationToken);
        return items.FirstOrDefault(x =>
            string.Equals(x.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SalvarClienteAsync(CrmCustomerResponse customer, CancellationToken cancellationToken = default)
    {
        var items = await _customers.LerAsync(cancellationToken);
        var index = items.FindIndex(x =>
            string.Equals(x.ExternalId, customer.ExternalId, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            items[index] = customer;
        }
        else
        {
            items.Add(customer);
        }

        await _customers.EscreverAsync(items, cancellationToken);
    }

    public async Task<CrmOrderResponse?> ObterPedidoAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var items = await _orders.LerAsync(cancellationToken);
        return items.FirstOrDefault(x =>
            string.Equals(x.OrderId, orderId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SalvarPedidoAsync(CrmOrderResponse order, CancellationToken cancellationToken = default)
    {
        var items = await _orders.LerAsync(cancellationToken);
        var index = items.FindIndex(x =>
            string.Equals(x.OrderId, order.OrderId, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            items[index] = order;
        }
        else
        {
            items.Add(order);
        }

        await _orders.EscreverAsync(items, cancellationToken);
    }
}
