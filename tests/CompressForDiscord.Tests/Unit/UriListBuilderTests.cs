using CompressForDiscord.Services.Clipboard;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class UriListBuilderTests
{
    [Fact]
    public void WindowsPath_BecomesProperFileUri()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // drive-letter semantics only exist on Windows
        }

        Assert.Equal("file:///C:/videos/clip.webm", UriListBuilder.ToFileUri(@"C:\videos\clip.webm"));
    }

    [Fact]
    public void SpacesUnicodeAndHash_AreEscaped()
    {
        string path = OperatingSystem.IsWindows()
            ? @"C:\dir with space\Ünï #2.webm"
            : "/dir with space/Ünï #2.webm";

        string uri = UriListBuilder.ToFileUri(path);

        Assert.Contains("%20", uri);       // space
        Assert.Contains("%23", uri);       // '#' must not become a fragment
        Assert.Contains("%C3%9C", uri);    // Ü, UTF-8 percent-encoded
        Assert.DoesNotContain(" ", uri);
        Assert.DoesNotContain("#", uri);
    }

    [Fact]
    public void UriList_UsesCrLfLineEnd()
    {
        string path = OperatingSystem.IsWindows() ? @"C:\a.png" : "/a.png";

        Assert.EndsWith("\r\n", UriListBuilder.BuildUriList(path));
    }
}
