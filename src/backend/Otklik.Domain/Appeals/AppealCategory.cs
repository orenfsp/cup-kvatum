namespace Otklik.Domain.Appeals;

public sealed class AppealCategory
{
    public static readonly Guid BullyingId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ConflictId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid PressureId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid UnsureId = Guid.Parse("20000000-0000-0000-0000-000000000004");

    public static readonly AppealCategory[] Initial =
    [
        new() { Id = BullyingId, Code = "bullying", DisplayName = "Травля", SortOrder = 10, IsActive = true },
        new() { Id = ConflictId, Code = "conflict", DisplayName = "Конфликт", SortOrder = 20, IsActive = true },
        new() { Id = PressureId, Code = "pressure", DisplayName = "Давление", SortOrder = 30, IsActive = true },
        new() { Id = UnsureId, Code = "unsure", DisplayName = "Не знаю, как это назвать", SortOrder = 40, IsActive = true }
    ];

    public Guid Id { get; set; }

    public required string Code { get; set; }

    public required string DisplayName { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UnixEpoch;
}
