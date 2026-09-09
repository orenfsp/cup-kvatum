using Otklik.Domain;
using Xunit;

namespace Otklik.UnitTests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void DomainAssembly_IsLoadable()
    {
        Assert.NotNull(typeof(AssemblyMarker).Assembly.FullName);
    }
}
