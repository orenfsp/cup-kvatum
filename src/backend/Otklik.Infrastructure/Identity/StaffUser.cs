using Microsoft.AspNetCore.Identity;

namespace Otklik.Infrastructure.Identity;

public sealed class StaffUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAvailable { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
