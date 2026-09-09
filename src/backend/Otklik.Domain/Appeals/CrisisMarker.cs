namespace Otklik.Domain.Appeals;

public sealed class CrisisMarker
{
    public Guid Id { get; set; }

    public required string Pattern { get; set; }

    public required string RiskType { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public static readonly CrisisMarker[] Initial =
    [
        Marker("60000000-0000-0000-0000-000000000001", "меня избива*", "PhysicalViolence", 10),
        Marker("60000000-0000-0000-0000-000000000002", "меня бьют", "PhysicalViolence", 20),
        Marker("60000000-0000-0000-0000-000000000003", "ударил* меня", "PhysicalViolence", 30),
        Marker("60000000-0000-0000-0000-000000000004", "угрожа* уб*", "LifeThreat", 40),
        Marker("60000000-0000-0000-0000-000000000005", "меня убьют", "LifeThreat", 50),
        Marker("60000000-0000-0000-0000-000000000006", "напал* нож*", "LifeThreat", 60),
        Marker("60000000-0000-0000-0000-000000000007", "хочу умер*", "SuicideRisk", 70),
        Marker("60000000-0000-0000-0000-000000000008", "не хочу жить", "SuicideRisk", 80),
        Marker("60000000-0000-0000-0000-000000000009", "покончи* с собой", "SuicideRisk", 90),
        Marker("60000000-0000-0000-0000-000000000010", "убью себя", "SuicideRisk", 100),
        Marker("60000000-0000-0000-0000-000000000011", "самоубий*", "SuicideRisk", 110),
        Marker("60000000-0000-0000-0000-000000000012", "суицид*", "SuicideRisk", 120)
    ];

    private static CrisisMarker Marker(string id, string pattern, string riskType, int sortOrder) => new()
    {
        Id = Guid.Parse(id),
        Pattern = pattern,
        RiskType = riskType,
        SortOrder = sortOrder,
        IsActive = true
    };
}
