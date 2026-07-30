using CaseIntegracao.Application.Services;
using CaseIntegracao.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CaseIntegracao.Api.Controllers;

[ApiController]
[Route("api/metrics")]
public sealed class MetricasController : ControllerBase
{
    private readonly ConsultaEventosService _queryService;

    public MetricasController(ConsultaEventosService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MetricasIntegracao), StatusCodes.Status200OK)]
    public ActionResult<MetricasIntegracao> Obter() => Ok(_queryService.ObterMetricas());
}
