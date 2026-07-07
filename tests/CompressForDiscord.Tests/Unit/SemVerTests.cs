using CompressForDiscord.Services;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class SemVerTests
{
    [Theory]
    [InlineData("0.1.0-rc.9", "0.1.0-rc.8")]   // rc numbers compared numerically (9 > 8)
    [InlineData("0.1.0-rc.10", "0.1.0-rc.9")]  // not lexical: 10 > 9
    [InlineData("0.1.0", "0.1.0-rc.8")]        // release outranks its prerelease
    [InlineData("0.2.0", "0.1.9")]             // minor beats patch
    [InlineData("1.0.0", "0.9.9")]             // major beats all
    [InlineData("0.1.0-rc.2", "0.1.0-rc.1.5")] // fewer-but-higher identifier still wins on first field
    public void Newer_ComparesGreater(string newer, string older)
    {
        Assert.True(SemVer.Compare(newer, older) > 0);
        Assert.True(SemVer.Compare(older, newer) < 0);
    }

    [Theory]
    [InlineData("0.1.0-rc.8", "0.1.0-rc.8")]
    [InlineData("0.1.0", "0.1.0")]
    [InlineData("0.1.0+abc123", "0.1.0")] // build metadata ignored
    public void Equal_ComparesZero(string a, string b)
    {
        Assert.Equal(0, SemVer.Compare(a, b));
    }

    [Fact]
    public void CurrentEqualsLatest_IsNotAnUpdate()
    {
        // The update checker treats >0 as "newer available"; equal must not trigger.
        Assert.False(SemVer.Compare("0.1.0-rc.8", "0.1.0-rc.8") > 0);
    }
}
