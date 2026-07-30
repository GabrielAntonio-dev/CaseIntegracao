namespace CaseIntegracao.Core.Domain.Entities;

public sealed class DadosCliente
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Document { get; init; }
}
