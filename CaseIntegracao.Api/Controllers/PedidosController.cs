using CaseIntegracao.Core.DTOs;
using CaseIntegracao.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CaseIntegracao.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class PedidosController : ControllerBase
{
    private readonly ConsultaEventosService _queryService;

    public PedidosController(ConsultaEventosService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(RespostaProjecaoPedido), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespostaProjecaoPedido>> Obter(string orderId, CancellationToken cancellationToken)
    {
        var item = await _queryService.ObterPedidoAsync(orderId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
