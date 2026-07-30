using CaseIntegracao.Core.Entities;
using CaseIntegracao.Core.Enums;
using CaseIntegracao.Core.Interfaces;

namespace CaseIntegracao.Core.Persistence;

public sealed class EventoIntegracaoArquivoRepository : IEventoIntegracaoRepository
{
    private readonly ArquivoJsonStore<EventoIntegracao> _store;

    public EventoIntegracaoArquivoRepository(ArquivoJsonStore<EventoIntegracao> store)
    {
        _store = store;
    }

    public async Task<EventoIntegracao?> ObterPorIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var items = await _store.LerAsync(cancellationToken);
        return items.FirstOrDefault(x => string.Equals(x.EventId, eventId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<EventoIntegracao>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _store.LerAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventoIntegracao>> ObterPendentesRetryAsync(
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var items = await _store.LerAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return items
            .Where(x =>
                x.Status == StatusProcessamentoEvento.Falhou &&
                x.AttemptCount < maxAttempts &&
                x.NextRetryAt.HasValue &&
                x.NextRetryAt.Value <= now)
            .OrderBy(x => x.NextRetryAt)
            .ToList();
    }

    public async Task SalvarAsync(EventoIntegracao integrationEvent, CancellationToken cancellationToken = default)
    {
        var items = await _store.LerAsync(cancellationToken);
        var index = items.FindIndex(x =>
            string.Equals(x.EventId, integrationEvent.EventId, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            items[index] = integrationEvent;
        }
        else
        {
            items.Add(integrationEvent);
        }

        await _store.EscreverAsync(items, cancellationToken);
    }
}
