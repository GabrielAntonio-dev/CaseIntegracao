namespace CaseIntegracao.Core.Infrastructure.Crm;

public sealed class CrmCustomerRequest
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Document { get; init; }
}

public sealed class CrmCustomerResponse
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Document { get; init; }
}

public sealed class CrmOrderRequest
{
    public required string OrderId { get; init; }
    public required string Status { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required string CustomerExternalId { get; init; }
}

public sealed class CrmOrderResponse
{
    public required string OrderId { get; init; }
    public required string Status { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required string CustomerExternalId { get; init; }
}
