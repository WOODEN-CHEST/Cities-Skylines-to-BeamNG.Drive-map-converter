using CsToBeamNG.Exporter.Core;
using Xunit;

namespace CsToBeamNG.Exporter.Tests.Core;

public sealed class ObjectFailuresTests
{
    [Fact]
    public void CountsEveryFailureButDescribesOnlyTheFirst()
    {
        var failures = new ObjectFailures(CountKeys.NetSegments, "network segments");

        Assert.Null(failures.Summary);
        for (int i = 0; i < ObjectFailures.MaxDetails + 5; i++)
        {
            failures.Add("Segment " + i);
        }

        Assert.Equal(ObjectFailures.MaxDetails + 5, failures.Count);
        Assert.Equal(ObjectFailures.MaxDetails, failures.Details.Count);
        Assert.Equal("Segment 0", failures.Details[0]);
        Assert.Equal("netSegments", failures.CountKey);
        Assert.Equal("25 network segments couldn't be exported, so the dump lacks them; see export.log.", failures.Summary);
    }
}
