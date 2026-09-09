namespace Otklik.Domain.Appeals;

public sealed class CrisisSupportContact
{
    public Guid Id { get; set; }

    public required string DisplayName { get; set; }

    public required string DisplayNumber { get; set; }

    public required string DialNumber { get; set; }

    public required string Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public static readonly CrisisSupportContact[] Initial =
    [
        new()
        {
            Id = Guid.Parse("61000000-0000-0000-0000-000000000001"),
            DisplayName = "Детский телефон доверия",
            DisplayNumber = "8-800-2000-122",
            DialNumber = "88002000122",
            Description = "Бесплатно и анонимно по России",
            SortOrder = 10,
            IsActive = true
        },
        new()
        {
            Id = Guid.Parse("61000000-0000-0000-0000-000000000002"),
            DisplayName = "Короткий номер телефона доверия",
            DisplayNumber = "124",
            DialNumber = "124",
            Description = "С мобильного телефона",
            SortOrder = 20,
            IsActive = true
        }
    ];
}
