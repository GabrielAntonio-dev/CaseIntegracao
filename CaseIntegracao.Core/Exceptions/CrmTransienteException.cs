namespace CaseIntegracao.Core.Exceptions;

public sealed class CrmTransienteException : Exception
{
    public int? StatusCode { get; }

    public CrmTransienteException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
