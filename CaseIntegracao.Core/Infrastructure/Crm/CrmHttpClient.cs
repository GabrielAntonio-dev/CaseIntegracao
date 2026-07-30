using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaseIntegracao.Core.Domain.Entities;
using CaseIntegracao.Core.Domain.Enums;
using CaseIntegracao.Core.Domain.Exceptions;
using CaseIntegracao.Core.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CaseIntegracao.Core.Infrastructure.Crm;

public sealed class CrmHttpClient : ICrmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<CrmHttpClient> _logger;

    public CrmHttpClient(HttpClient httpClient, ILogger<CrmHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task GarantirClienteAsync(DadosCliente customer, CancellationToken cancellationToken = default)
    {
        var existing = await ObterClienteAsync(customer.ExternalId, cancellationToken);
        var payload = new CrmCustomerRequest
        {
            ExternalId = customer.ExternalId,
            Name = customer.Name,
            Email = customer.Email,
            Document = customer.Document
        };

        if (existing is null)
        {
            using var response = await _httpClient.PostAsJsonAsync("/crm/customers", payload, JsonOptions, cancellationToken);
            await GarantirSucessoOuLancarAsync(response, cancellationToken);
            return;
        }

        using var updateResponse = await _httpClient.PutAsJsonAsync(
            $"/crm/customers/{Uri.EscapeDataString(customer.ExternalId)}",
            payload,
            JsonOptions,
            cancellationToken);
        await GarantirSucessoOuLancarAsync(updateResponse, cancellationToken);
    }

    public async Task UpsertPedidoAsync(
        string orderId,
        StatusPedido status,
        decimal totalAmount,
        string currency,
        string customerExternalId,
        CancellationToken cancellationToken = default)
    {
        var existing = await ObterPedidoAsync(orderId, cancellationToken);
        var payload = new CrmOrderRequest
        {
            OrderId = orderId,
            Status = ParaStatusCrm(status),
            TotalAmount = totalAmount,
            Currency = currency,
            CustomerExternalId = customerExternalId
        };

        if (existing is null)
        {
            using var createResponse = await _httpClient.PostAsJsonAsync("/crm/orders", payload, JsonOptions, cancellationToken);
            await GarantirSucessoOuLancarAsync(createResponse, cancellationToken);
            return;
        }

        using var updateResponse = await _httpClient.PutAsJsonAsync(
            $"/crm/orders/{Uri.EscapeDataString(orderId)}",
            payload,
            JsonOptions,
            cancellationToken);
        await GarantirSucessoOuLancarAsync(updateResponse, cancellationToken);
    }

    private static string ParaStatusCrm(StatusPedido status) =>
        status switch
        {
            StatusPedido.Pendente => "pending",
            StatusPedido.Confirmado => "confirmed",
            StatusPedido.Cancelado => "canceled",
            _ => status.ToString().ToLowerInvariant()
        };

    private async Task<CrmCustomerResponse?> ObterClienteAsync(string externalId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/crm/customers/{Uri.EscapeDataString(externalId)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await GarantirSucessoOuLancarAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CrmCustomerResponse>(JsonOptions, cancellationToken);
    }

    private async Task<CrmOrderResponse?> ObterPedidoAsync(string orderId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/crm/orders/{Uri.EscapeDataString(orderId)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await GarantirSucessoOuLancarAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CrmOrderResponse>(JsonOptions, cancellationToken);
    }

    private async Task GarantirSucessoOuLancarAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;

        if (statusCode == 429 || statusCode >= 500)
        {
            _logger.LogWarning("Falha transitória no CRM. StatusCode={StatusCode}, Body={Body}", statusCode, body);
            throw new CrmTransienteException($"CRM retornou {statusCode}: {body}", statusCode);
        }

        throw new InvalidOperationException($"CRM retornou status inesperado {statusCode}: {body}");
    }
}
