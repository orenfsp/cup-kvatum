namespace Otklik.Domain.Analytics;

public sealed class AnalyticsExport
{
    public Guid Id { get; set; }

    public Guid RequestedByUserId { get; set; }

    public required string RequestedByRole { get; set; }

    public required string Format { get; set; }

    public DateTimeOffset PeriodFrom { get; set; }

    public DateTimeOffset PeriodTo { get; set; }

    public required string FilePath { get; set; }

    public int RowCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? DownloadedAt { get; set; }
}
