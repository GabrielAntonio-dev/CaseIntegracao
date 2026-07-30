using CaseIntegracao.Core.Infrastructure.Crm;
using Microsoft.AspNetCore.Mvc;

namespace CaseIntegracao.Api.Controllers;

[ApiController]
[Route("crm")]
public sealed class CrmMockController : ControllerBase
{
    private readonly CrmMockStore _store;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CrmMockController> _logger;

    public CrmMockController(
        CrmMockStore store,
        IConfiguration configuration,
        ILogger<CrmMockController> logger)
    {
        _store = store;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("customers/{externalId}")]
    public async Task<ActionResult<CrmCustomerResponse>> ObterCliente(string externalId, CancellationToken cancellationToken)
    {
        if (await TalvezFalharAsync(cancellationToken))
        {
            return StatusCode(await EscolherStatusFalhaAsync());
        }

        var customer = await _store.ObterClienteAsync(externalId, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost("customers")]
    public async Task<ActionResult<CrmCustomerResponse>> CriarCliente(
        [FromBody] CrmCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (await TalvezFalharAsync(cancellationToken))
        {
            return StatusCode(await EscolherStatusFalhaAsync());
        }

        var existing = await _store.ObterClienteAsync(request.ExternalId, cancellationToken);
        if (existing is not null)
        {
            return Conflict(new { error = "Cliente já existe." });
        }

        var created = new CrmCustomerResponse
        {
            ExternalId = request.ExternalId,
            Name = request.Name,
            Email = request.Email,
            Document = request.Document
        };

        await _store.SalvarClienteAsync(created, cancellationToken);
        return CreatedAtAction(nameof(ObterCliente), new { externalId = created.ExternalId }, created);
    }

    [HttpPut("customers/{externalId}")]
    public async Task<ActionResult<CrmCustomerResponse>> AtualizarCliente(
        string externalId,
        [FromBody] CrmCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (await TalvezFalharAsync(cancellationToken))
        {
            return StatusCode(await EscolherStatusFalhaAsync());
        }

        var updated = new CrmCustomerResponse
        {
            ExternalId = externalId,
            Name = request.Name,
            Email = request.Email,
            Document = request.Document
        };

        await _store.SalvarClienteAsync(updated, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("orders/{orderId}")]
    public async Task<ActionResult<CrmOrderResponse>> ObterPedido(string orderId, CancellationToken cancellationToken)
    {
        if (await TalvezFalharAsync(cancellationToken))
        {
            return StatusCode(await EscolherStatusFalhaAsync());
        }

        var order = await _store.ObterPedidoAsync(orderId, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("orders")]
    public async Task<ActionResult<CrmOrderResponse>> CriarPedido(
        [FromBody] CrmOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (await TalvezFalharAsync(cancellationToken))
        {
            return StatusCode(await EscolherStatusFalhaAsync());
        }

        var existing = await _store.ObterPedidoAsync(request.OrderId, cancellationToken);
        if (existing is not null)
        {
            return Conflict(new { error = "Pedido já existe." });
        }

        var created = new CrmOrderResponse
        {
            OrderId = request.OrderId,
            Status = request.Status,
            TotalAmount = request.TotalAmount,
            Currency = request.Currency,
            CustomerExternalId = request.CustomerExternalId
        };

        await _store.SalvarPedidoAsync(created, cancellationToken);
        return CreatedAtAction(nameof(ObterPedido), new { orderId = created.OrderId }, created);
    }

    [HttpPut("orders/{orderId}")]
    public async Task<ActionResult<CrmOrderResponse>> AtualizarPedido(
        string orderId,
        [FromBody] CrmOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (await TalvezFalharAsync(cancellationToken))
        {
            return StatusCode(await EscolherStatusFalhaAsync());
        }

        var updated = new CrmOrderResponse
        {
            OrderId = orderId,
            Status = request.Status,
            TotalAmount = request.TotalAmount,
            Currency = request.Currency,
            CustomerExternalId = request.CustomerExternalId
        };

        await _store.SalvarPedidoAsync(updated, cancellationToken);
        return Ok(updated);
    }

    private async Task<bool> TalvezFalharAsync(CancellationToken cancellationToken)
    {
        var failureRate = _configuration.GetValue("CrmMock:FailureRate", 0.15);
        if (Random.Shared.NextDouble() >= failureRate)
        {
            return false;
        }

        var mode = Random.Shared.Next(0, 3);
        if (mode == 0)
        {
            // Delay acima do HttpClient.Timeout (10s) para gerar TaskCanceledException no cliente.
            var timeoutDelaySeconds = _configuration.GetValue("CrmMock:TimeoutDelaySeconds", 12);
            _logger.LogWarning(
                "CRM mock simulando timeout ({TimeoutDelaySeconds}s)",
                timeoutDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(timeoutDelaySeconds), cancellationToken);
            return true;
        }

        _logger.LogWarning("CRM mock simulando falha HTTP");
        return true;
    }

    private Task<int> EscolherStatusFalhaAsync()
    {
        var status = Random.Shared.Next(0, 2) == 0 ? 429 : 500;
        return Task.FromResult(status);
    }
}
