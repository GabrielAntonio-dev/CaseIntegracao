using CaseIntegracao.Core.DTOs;
using CaseIntegracao.Core.Mapping;
using CaseIntegracao.Core.Interfaces;

namespace CaseIntegracao.Core.Services;

public sealed class ConsultaEventosService
{
    private readonly IEventoIntegracaoRepository _eventRepository;
    private readonly IProjecaoPedidoRepository _orderRepository;
    private readonly IMetricasCollector _metrics;

    public ConsultaEventosService(
        IEventoIntegracaoRepository eventRepository,
        IProjecaoPedidoRepository orderRepository,
        IMetricasCollector metrics)
    {
        _eventRepository = eventRepository;
        _orderRepository = orderRepository;
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<RespostaEventoIntegracao>> ListarEventosAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _eventRepository.ObterTodosAsync(cancellationToken);
        return events
            .OrderByDescending(e => e.ReceivedAt)
            .Select(WebhookMapper.ParaResposta)
            .ToList();
    }

    public async Task<RespostaEventoIntegracao?> ObterEventoAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _eventRepository.ObterPorIdAsync(eventId, cancellationToken);
        return entity is null ? null : WebhookMapper.ParaResposta(entity);
    }

    public async Task<RespostaProjecaoPedido?> ObterPedidoAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var projection = await _orderRepository.ObterPorIdAsync(orderId, cancellationToken);
        return projection is null ? null : WebhookMapper.ParaResposta(projection);
    }

    public MetricasIntegracao ObterMetricas() => _metrics.Snapshot();
}
