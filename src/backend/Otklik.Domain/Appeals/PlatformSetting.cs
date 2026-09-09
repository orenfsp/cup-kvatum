namespace Otklik.Domain.Appeals;

public sealed class PlatformSetting
{
    public const string AutoCloseDaysKey = "Appeal.AutoCloseDays";

    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
