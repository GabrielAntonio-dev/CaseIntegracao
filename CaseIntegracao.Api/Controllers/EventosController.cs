using CaseIntegracao.Core.DTOs;
using CaseIntegracao.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CaseIntegracao.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventosController : ControllerBase
{
    private readonly ConsultaEventosService _queryService;

    public EventosController(ConsultaEventosService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RespostaEventoIntegracao>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RespostaEventoIntegracao>>> Listar(CancellationToken cancellationToken)
    {
        return Ok(await _queryService.ListarEventosAsync(cancellationToken));
    }

    [HttpGet("{eventId}")]
    [ProducesResponseType(typeof(RespostaEventoIntegracao), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespostaEventoIntegracao>> Obter(string eventId, CancellationToken cancellationToken)
    {
        var item = await _queryService.ObterEventoAsync(eventId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
