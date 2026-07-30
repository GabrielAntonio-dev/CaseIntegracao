using CaseIntegracao.Core.Domain.Entities;

namespace CaseIntegracao.Core.Domain.Services;

public static class PoliticaOrdenacaoEventos
{
    public static bool DeveAplicar(ProjecaoPedido? atual, DateTimeOffset occurredAt) =>
        atual is null || atual.EhMaisNovoOuIgual(occurredAt);
}
