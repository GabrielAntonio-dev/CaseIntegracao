using CaseIntegracao.Core.DTOs;
using CaseIntegracao.Core.Mapping;
using CaseIntegracao.Core.Options;
using CaseIntegracao.Core.Entities;
using CaseIntegracao.Core.Exceptions;
using CaseIntegracao.Core.Interfaces;
using CaseIntegracao.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CaseIntegracao.Core.Services;

public sealed class EventoPedidoProcessor
{
    private readonly IEventoIntegracaoRepository _eventRepository;
    private readonly IProjecaoPedidoRepository _orderRepository;
    private readonly ICrmClient _crmClient;
    private readonly IMetricasCollector _metrics;
    private readonly RetryOptions _retryOptions;
    private readonly ILogger<EventoPedidoProcessor> _logger;

    public EventoPedidoProcessor(
        IEventoIntegracaoRepository eventRepository,
        IProjecaoPedidoRepository orderRepository,
        ICrmClient crmClient,
        IMetricasCollector metrics,
        IOptions<RetryOptions> retryOptions,
        ILogger<EventoPedidoProcessor> logger)
    {
        _eventRepository = eventRepository;
        _orderRepository = orderRepository;
        _crmClient = crmClient;
        _metrics = metrics;
        _retryOptions = retryOptions.Value;
        _logger = logger;
    }

    public async Task<ResultadoProcessamentoEvento> ReceberAsync(
        RequisicaoWebhookPedido request,
        CancellationToken cancellationToken = default)
    {
        EventoIntegracao incoming;
        try
        {
            incoming = WebhookMapper.ParaDominio(request);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        var existing = await _eventRepository.ObterPorIdAsync(incoming.EventId, cancellationToken);
        if (existing is not null && existing.EhSucessoTerminal)
        {
            _metrics.IncrementarIdempotentes();
            _logger.LogInformation(
                "Evento {EventId} já processado com status {Status} (idempotência)",
                existing.EventId,
                existing.Status);

            return new ResultadoProcessamentoEvento
            {
                EventId = existing.EventId,
                Status = existing.Status.ToString(),
                AlreadyProcessed = true,
                Message = "Evento já processado."
            };
        }

        if (existing is null)
        {
            _metrics.IncrementarRecebidos();
            await _eventRepository.SalvarAsync(incoming, cancellationToken);
            existing = incoming;
        }

        await ProcessarInternoAsync(existing, ehRetry: false, cancellationToken);

        var refreshed = await _eventRepository.ObterPorIdAsync(existing.EventId, cancellationToken)
            ?? existing;

        return new ResultadoProcessamentoEvento
        {
            EventId = refreshed.EventId,
            Status = refreshed.Status.ToString(),
            AlreadyProcessed = false,
            Message = refreshed.LastError
        };
    }

    public async Task ProcessarRetriesPendentesAsync(CancellationToken cancellationToken = default)
    {
        var dueEvents = await _eventRepository.ObterPendentesRetryAsync(_retryOptions.MaxAttempts, cancellationToken);
        foreach (var integrationEvent in dueEvents)
        {
            _metrics.IncrementarRetries();
            _logger.LogInformation(
                "Retentando evento {EventId}, tentativa {AttemptCount}",
                integrationEvent.EventId,
                integrationEvent.AttemptCount + 1);

            await ProcessarInternoAsync(integrationEvent, ehRetry: true, cancellationToken);
        }
    }

    public async Task ProcessarInternoAsync(
        EventoIntegracao integrationEvent,
        bool ehRetry,
        CancellationToken cancellationToken = default)
    {
        if (integrationEvent.EhSucessoTerminal)
        {
            return;
        }

        integrationEvent.MarcarProcessando();
        await _eventRepository.SalvarAsync(integrationEvent, cancellationToken);

        try
        {
            var projection = await _orderRepository.ObterPorIdAsync(integrationEvent.OrderId, cancellationToken);
            if (!PoliticaOrdenacaoEventos.DeveAplicar(projection, integrationEvent.OccurredAt))
            {
                integrationEvent.MarcarIgnoradoObsoleto();
                await _eventRepository.SalvarAsync(integrationEvent, cancellationToken);
                _metrics.IncrementarIgnoradosObsoletos();

                _logger.LogInformation(
                    "Evento obsoleto {EventId} ignorado para pedido {OrderId}. OccurredAt={OccurredAt}, LastOccurredAt={LastOccurredAt}",
                    integrationEvent.EventId,
                    integrationEvent.OrderId,
                    integrationEvent.OccurredAt,
                    projection!.LastOccurredAt);

                return;
            }

            await _crmClient.GarantirClienteAsync(integrationEvent.Customer, cancellationToken);
            await _crmClient.UpsertPedidoAsync(
                integrationEvent.OrderId,
                integrationEvent.OrderStatus,
                integrationEvent.TotalAmount,
                integrationEvent.Currency,
                integrationEvent.Customer.ExternalId,
                cancellationToken);

            if (projection is null)
            {
                projection = new ProjecaoPedido
                {
                    OrderId = integrationEvent.OrderId,
                    Status = integrationEvent.OrderStatus,
                    TotalAmount = integrationEvent.TotalAmount,
                    Currency = integrationEvent.Currency,
                    Customer = integrationEvent.Customer,
                    LastOccurredAt = integrationEvent.OccurredAt
                };
            }
            else
            {
                projection.Aplicar(
                    integrationEvent.OrderStatus,
                    integrationEvent.TotalAmount,
                    integrationEvent.Currency,
                    integrationEvent.Customer,
                    integrationEvent.OccurredAt);
            }

            await _orderRepository.SalvarAsync(projection, cancellationToken);

            integrationEvent.MarcarSincronizado();
            await _eventRepository.SalvarAsync(integrationEvent, cancellationToken);
            _metrics.IncrementarSincronizados();

            _logger.LogInformation(
                "Evento {EventId} sincronizado para pedido {OrderId}",
                integrationEvent.EventId,
                integrationEvent.OrderId);
        }
        catch (CrmTransienteException ex)
        {
            await TratarFalhaTransienteAsync(integrationEvent, ex.Message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            await TratarFalhaTransienteAsync(integrationEvent, ex.Message, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            await TratarFalhaTransienteAsync(integrationEvent, "Timeout no CRM: " + ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada ao processar evento {EventId}", integrationEvent.EventId);
            await TratarFalhaTransienteAsync(integrationEvent, ex.Message, cancellationToken);
        }
    }

    private async Task TratarFalhaTransienteAsync(
        EventoIntegracao integrationEvent,
        string error,
        CancellationToken cancellationToken)
    {
        _metrics.IncrementarFalhas();

        if (integrationEvent.AttemptCount >= _retryOptions.MaxAttempts)
        {
            integrationEvent.MarcarCartaMortua(error);
            await _eventRepository.SalvarAsync(integrationEvent, cancellationToken);

            _logger.LogError(
                "Evento {EventId} enviado para carta morta após {AttemptCount} tentativas. Erro={Error}",
                integrationEvent.EventId,
                integrationEvent.AttemptCount,
                error);
            return;
        }

        var delay = CalcularBackoff(integrationEvent.AttemptCount);
        var nextRetry = DateTimeOffset.UtcNow.Add(delay);
        integrationEvent.MarcarFalhou(error, nextRetry);
        await _eventRepository.SalvarAsync(integrationEvent, cancellationToken);

        _logger.LogWarning(
            "Evento {EventId} falhou (tentativa {AttemptCount}). NextRetryAt={NextRetryAt}. Erro={Error}",
            integrationEvent.EventId,
            integrationEvent.AttemptCount,
            nextRetry,
            error);
    }

    private TimeSpan CalcularBackoff(int attemptCount)
    {
        var exponential = _retryOptions.BaseDelaySeconds * Math.Pow(2, Math.Max(0, attemptCount - 1));
        var capped = Math.Min(exponential, _retryOptions.MaxDelaySeconds);
        var jitterMs = Random.Shared.Next(0, 500);
        return TimeSpan.FromSeconds(capped) + TimeSpan.FromMilliseconds(jitterMs);
    }
}
