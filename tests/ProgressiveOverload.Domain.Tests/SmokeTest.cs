using Shouldly;

namespace ProgressiveOverload.Domain.Tests;

public class SmokeTest
{
    [Fact]
    public void TestHarnessRuns()
    {
        (2 + 2).ShouldBe(4);
    }
}
