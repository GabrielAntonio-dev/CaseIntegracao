namespace CaseIntegracao.Core.Application.Options;

public sealed class RetryOptions
{
    public const string SectionName = "Retry";

    public int MaxAttempts { get; set; } = 5;
    public int BaseDelaySeconds { get; set; } = 2;
    public int MaxDelaySeconds { get; set; } = 60;
    public int PollIntervalSeconds { get; set; } = 3;
}
