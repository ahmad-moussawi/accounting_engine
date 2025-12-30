using TUnit.Core;

namespace Tests;

public class SimpleTest
{
    [Test]
    public async Task MyTest()
    {
        await Assert.That(1).IsEqualTo(1);
    }
}
