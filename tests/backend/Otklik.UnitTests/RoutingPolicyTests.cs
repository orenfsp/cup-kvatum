using Otklik.Application.Appeals;
using Xunit;

namespace Otklik.UnitTests;

public sealed class RoutingPolicyTests
{
    [Fact]
    public void Empty_group_requires_admin_attention()
    {
        var result = RoutingPolicy.Evaluate(Guid.NewGuid(), "Группа", 3, []);

        Assert.Empty(result.Experts);
        Assert.Contains("администратор", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Experts_are_ranked_by_active_load()
    {
        var lessBusy = Guid.NewGuid();
        var result = RoutingPolicy.Evaluate(
            Guid.NewGuid(),
            "Группа",
            5,
            [
                new ExpertLoad(Guid.NewGuid(), "Первый", 4),
                new ExpertLoad(lessBusy, "Второй", 1)
            ]);

        Assert.Equal(lessBusy, result.Experts[0].Id);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void Full_group_requires_manual_override()
    {
        var result = RoutingPolicy.Evaluate(
            Guid.NewGuid(),
            "Группа",
            2,
            [new ExpertLoad(Guid.NewGuid(), "Эксперт", 2)]);

        Assert.True(result.Experts[0].AtCapacity);
        Assert.Contains("лимита", result.Warning, StringComparison.OrdinalIgnoreCase);
    }
}
