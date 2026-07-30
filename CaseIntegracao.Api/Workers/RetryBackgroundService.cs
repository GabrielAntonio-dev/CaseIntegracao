using CaseIntegracao.Core.Application.Options;
using CaseIntegracao.Core.Application.Services;
using Microsoft.Extensions.Options;

namespace CaseIntegracao.Api.Workers;

public sealed class RetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RetryOptions _options;
    private readonly ILogger<RetryBackgroundService> _logger;

    public RetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<RetryOptions> options,
        ILogger<RetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<EventoPedidoProcessor>();
                await processor.ProcessarRetriesPendentesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker de retry falhou inesperadamente");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
