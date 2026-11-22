namespace SmartLawyer.Core.Tests;

// Placeholder until the first Core algorithms land in M2.
public class AssemblySmokeTests
{
    [Fact]
    public void CoreAssembly_Loads()
    {
        Assert.Equal("SmartLawyer.Core", typeof(SmartLawyer.Core.AssemblyMarker).Assembly.GetName().Name);
    }
}
