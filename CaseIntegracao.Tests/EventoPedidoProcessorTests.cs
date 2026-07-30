using CaseIntegracao.Application.DTOs;
using CaseIntegracao.Application.Options;
using CaseIntegracao.Application.Services;
using CaseIntegracao.Domain.Entities;
using CaseIntegracao.Domain.Enums;
using CaseIntegracao.Domain.Exceptions;
using CaseIntegracao.Domain.Interfaces;
using CaseIntegracao.Infrastructure.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CaseIntegracao.Tests;

public sealed class EventoPedidoProcessorTests
{
    [Fact]
    public async Task ReceberAsync_MesmoEventoDuasVezes_EhIdempotente()
    {
        var events = new EventoRepositoryEmMemoria();
        var orders = new PedidoRepositoryEmMemoria();
        var crm = new Mock<ICrmClient>(MockBehavior.Strict);
        crm.Setup(x => x.GarantirClienteAsync(It.IsAny<DadosCliente>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        crm.Setup(x => x.UpsertPedidoAsync(
                It.IsAny<string>(),
                It.IsAny<StatusPedido>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = CriarProcessor(events, orders, crm.Object);
        var request = CriarRequisicao("evt-1", "ord-1", "order.created", "pending", DateTimeOffset.UtcNow);

        var first = await processor.ReceberAsync(request);
        var second = await processor.ReceberAsync(request);

        Assert.Equal("Sincronizado", first.Status);
        Assert.True(second.AlreadyProcessed);
        Assert.Equal("Sincronizado", second.Status);

        crm.Verify(x => x.GarantirClienteAsync(It.IsAny<DadosCliente>(), It.IsAny<CancellationToken>()), Times.Once);
        crm.Verify(x => x.UpsertPedidoAsync(
            "ord-1",
            StatusPedido.Pendente,
            It.IsAny<decimal>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceberAsync_EventosForaDeOrdem_MantemEstadoMaisNovo()
    {
        var events = new EventoRepositoryEmMemoria();
        var orders = new PedidoRepositoryEmMemoria();
        var crm = new Mock<ICrmClient>();
        crm.Setup(x => x.GarantirClienteAsync(It.IsAny<DadosCliente>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        crm.Setup(x => x.UpsertPedidoAsync(
                It.IsAny<string>(),
                It.IsAny<StatusPedido>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = CriarProcessor(events, orders, crm.Object);
        var newer = DateTimeOffset.Parse("2026-07-27T18:10:00Z");
        var older = DateTimeOffset.Parse("2026-07-27T18:00:00Z");

        await processor.ReceberAsync(CriarRequisicao("evt-new", "ord-9", "order.updated", "confirmed", newer, 200m));
        await processor.ReceberAsync(CriarRequisicao("evt-old", "ord-9", "order.created", "pending", older, 100m));

        var projection = await orders.ObterPorIdAsync("ord-9");
        Assert.NotNull(projection);
        Assert.Equal(StatusPedido.Confirmado, projection!.Status);
        Assert.Equal(200m, projection.TotalAmount);
        Assert.Equal(newer, projection.LastOccurredAt);

        var stale = await events.ObterPorIdAsync("evt-old");
        Assert.Equal(StatusProcessamentoEvento.IgnoradoObsoleto, stale!.Status);
    }

    [Fact]
    public async Task ReceberAsync_OrderCanceled_ForcaStatusCancelado()
    {
        var events = new EventoRepositoryEmMemoria();
        var orders = new PedidoRepositoryEmMemoria();
        var crm = new Mock<ICrmClient>();
        crm.Setup(x => x.GarantirClienteAsync(It.IsAny<DadosCliente>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        crm.Setup(x => x.UpsertPedidoAsync(
                It.IsAny<string>(),
                It.IsAny<StatusPedido>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = CriarProcessor(events, orders, crm.Object);
        var createdAt = DateTimeOffset.Parse("2026-07-27T18:00:00Z");
        var canceledAt = DateTimeOffset.Parse("2026-07-27T18:20:00Z");

        await processor.ReceberAsync(CriarRequisicao("evt-c1", "ord-cancel", "order.created", "pending", createdAt));
        await processor.ReceberAsync(CriarRequisicao("evt-c2", "ord-cancel", "order.canceled", "confirmed", canceledAt));

        var projection = await orders.ObterPorIdAsync("ord-cancel");
        Assert.NotNull(projection);
        Assert.Equal(StatusPedido.Cancelado, projection!.Status);

        crm.Verify(x => x.UpsertPedidoAsync(
            "ord-cancel",
            StatusPedido.Cancelado,
            It.IsAny<decimal>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceberAsync_FalhaTransienteCrm_AgendaRetryESincroniza()
    {
        var events = new EventoRepositoryEmMemoria();
        var orders = new PedidoRepositoryEmMemoria();
        var crm = new Mock<ICrmClient>();
        var callCount = 0;

        crm.Setup(x => x.GarantirClienteAsync(It.IsAny<DadosCliente>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new CrmTransienteException("CRM indisponível", 500);
                }

                return Task.CompletedTask;
            });

        crm.Setup(x => x.UpsertPedidoAsync(
                It.IsAny<string>(),
                It.IsAny<StatusPedido>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = CriarProcessor(events, orders, crm.Object, maxAttempts: 5, baseDelaySeconds: 0);
        var request = CriarRequisicao("evt-retry", "ord-retry", "order.created", "pending", DateTimeOffset.UtcNow);

        var first = await processor.ReceberAsync(request);
        Assert.Equal("Falhou", first.Status);

        var failed = await events.ObterPorIdAsync("evt-retry");
        Assert.NotNull(failed);
        failed!.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await events.SalvarAsync(failed);

        await processor.ProcessarRetriesPendentesAsync();

        var synced = await events.ObterPorIdAsync("evt-retry");
        Assert.Equal(StatusProcessamentoEvento.Sincronizado, synced!.Status);
        Assert.True(callCount >= 2);
    }

    private static EventoPedidoProcessor CriarProcessor(
        IEventoIntegracaoRepository events,
        IProjecaoPedidoRepository orders,
        ICrmClient crm,
        int maxAttempts = 5,
        int baseDelaySeconds = 2)
    {
        var options = Options.Create(new RetryOptions
        {
            MaxAttempts = maxAttempts,
            BaseDelaySeconds = baseDelaySeconds,
            MaxDelaySeconds = 1,
            PollIntervalSeconds = 1
        });

        return new EventoPedidoProcessor(
            events,
            orders,
            crm,
            new MetricasEmMemoriaCollector(),
            options,
            NullLogger<EventoPedidoProcessor>.Instance);
    }

    private static RequisicaoWebhookPedido CriarRequisicao(
        string eventId,
        string orderId,
        string eventType,
        string status,
        DateTimeOffset occurredAt,
        decimal amount = 150.90m) =>
        new()
        {
            EventId = eventId,
            EventType = eventType,
            OccurredAt = occurredAt,
            Data = new DadosWebhookPedido
            {
                OrderId = orderId,
                Status = status,
                TotalAmount = amount,
                Currency = "BRL",
                Customer = new DadosWebhookCliente
                {
                    ExternalId = "cust-1",
                    Name = "Ana Silva",
                    Email = "ana@email.com",
                    Document = "12345678901"
                }
            }
        };

    private sealed class EventoRepositoryEmMemoria : IEventoIntegracaoRepository
    {
        private readonly Dictionary<string, EventoIntegracao> _items = new(StringComparer.OrdinalIgnoreCase);

        public Task<EventoIntegracao?> ObterPorIdAsync(string eventId, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(eventId, out var item);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyList<EventoIntegracao>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventoIntegracao>>(_items.Values.ToList());

        public Task<IReadOnlyList<EventoIntegracao>> ObterPendentesRetryAsync(
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var due = _items.Values
                .Where(x =>
                    x.Status == StatusProcessamentoEvento.Falhou &&
                    x.AttemptCount < maxAttempts &&
                    x.NextRetryAt.HasValue &&
                    x.NextRetryAt.Value <= now)
                .ToList();

            return Task.FromResult<IReadOnlyList<EventoIntegracao>>(due);
        }

        public Task SalvarAsync(EventoIntegracao integrationEvent, CancellationToken cancellationToken = default)
        {
            _items[integrationEvent.EventId] = integrationEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class PedidoRepositoryEmMemoria : IProjecaoPedidoRepository
    {
        private readonly Dictionary<string, ProjecaoPedido> _items = new(StringComparer.OrdinalIgnoreCase);

        public Task<ProjecaoPedido?> ObterPorIdAsync(string orderId, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(orderId, out var item);
            return Task.FromResult(item);
        }

        public Task SalvarAsync(ProjecaoPedido projection, CancellationToken cancellationToken = default)
        {
            _items[projection.OrderId] = projection;
            return Task.CompletedTask;
        }
    }
}
