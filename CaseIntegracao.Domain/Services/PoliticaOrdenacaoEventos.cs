using CaseIntegracao.Domain.Entities;

namespace CaseIntegracao.Domain.Services;

public static class PoliticaOrdenacaoEventos
{
    public static bool DeveAplicar(ProjecaoPedido? atual, DateTimeOffset occurredAt) =>
        atual is null || atual.EhMaisNovoOuIgual(occurredAt);
}
