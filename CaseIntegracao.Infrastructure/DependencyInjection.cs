using CaseIntegracao.Domain.Entities;
using CaseIntegracao.Domain.Interfaces;
using CaseIntegracao.Infrastructure.Crm;
using CaseIntegracao.Infrastructure.Metrics;
using CaseIntegracao.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace CaseIntegracao.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var dataPath = configuration["Storage:DataPath"]
            ?? Path.Combine(contentRootPath, "data");
        Directory.CreateDirectory(dataPath);

        var crmDataPath = Path.Combine(dataPath, "crm");
        Directory.CreateDirectory(crmDataPath);

        services.AddSingleton(new ArquivoJsonStore<EventoIntegracao>(Path.Combine(dataPath, "events.json")));
        services.AddSingleton(new ArquivoJsonStore<ProjecaoPedido>(Path.Combine(dataPath, "orders.json")));
        services.AddSingleton(new CrmMockStore(crmDataPath));

        services.AddSingleton<IEventoIntegracaoRepository, EventoIntegracaoArquivoRepository>();
        services.AddSingleton<IProjecaoPedidoRepository, ProjecaoPedidoArquivoRepository>();
        services.AddSingleton<IMetricasCollector, MetricasEmMemoriaCollector>();

        var crmBaseUrl = configuration["Crm:BaseUrl"] ?? "http://localhost:5080";

        services.AddHttpClient<ICrmClient, CrmHttpClient>(client =>
            {
                client.BaseAddress = new Uri(crmBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddPolicyHandler(ObterPoliticaRetry())
            .AddPolicyHandler(ObterPoliticaCircuitBreaker());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> ObterPoliticaRetry()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)));
    }

    private static IAsyncPolicy<HttpResponseMessage> ObterPoliticaCircuitBreaker()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(15));
    }
}
