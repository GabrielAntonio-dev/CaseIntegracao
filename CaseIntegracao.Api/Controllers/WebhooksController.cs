using CaseIntegracao.Application.DTOs;
using CaseIntegracao.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CaseIntegracao.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly EventoPedidoProcessor _processor;

    public WebhooksController(EventoPedidoProcessor processor)
    {
        _processor = processor;
    }

    /// <summary>
    /// Recebe eventos de webhook de pedidos do sistema de origem.
    /// </summary>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(ResultadoProcessamentoEvento), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultadoProcessamentoEvento), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceberEventoPedido(
        [FromBody] RequisicaoWebhookPedido request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var effectiveRequest = request;
        if (string.IsNullOrWhiteSpace(request.EventId) && !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            effectiveRequest = new RequisicaoWebhookPedido
            {
                EventId = idempotencyKey.Trim(),
                EventType = request.EventType,
                OccurredAt = request.OccurredAt,
                Data = request.Data
            };
        }

        try
        {
            var result = await _processor.ReceberAsync(effectiveRequest, cancellationToken);
            if (result.AlreadyProcessed ||
                result.Status is "Sincronizado" or "IgnoradoObsoleto")
            {
                return Ok(result);
            }

            return Accepted(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
