using CaseIntegracao.Core.Domain.Entities;

namespace CaseIntegracao.Core.Domain.Interfaces;

public interface IEventoIntegracaoRepository
{
    Task<EventoIntegracao?> ObterPorIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventoIntegracao>> ObterTodosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventoIntegracao>> ObterPendentesRetryAsync(int maxAttempts, CancellationToken cancellationToken = default);
    Task SalvarAsync(EventoIntegracao integrationEvent, CancellationToken cancellationToken = default);
}
