using System.Linq;
using CompressForDiscord.Services.Planning;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class OutputNamerTests
{
    [Fact]
    public void Video_MapsToDiscordWebm_WithCollisionSuffixes()
    {
        var names = OutputNamer.CandidateFileNames("video.mp4", "webm").Take(3).ToArray();

        Assert.Equal(["video.discord.webm", "video.discord (2).webm", "video.discord (3).webm"], names);
    }

    [Fact]
    public void Image_MapsToDiscordPng()
    {
        Assert.Equal("photo.discord.png", OutputNamer.CandidateFileNames("photo.jpg", "png").First());
    }

    [Fact]
    public void MultiDotNames_KeepEverythingButTheExtension()
    {
        Assert.Equal("my.video.file.discord.webm",
            OutputNamer.CandidateFileNames("my.video.file.mp4", "webm").First());
    }

    [Fact]
    public void UnicodeAndSpaces_SurviveIntact()
    {
        Assert.Equal("Ünïcode video (final) #2.discord.webm",
            OutputNamer.CandidateFileNames("Ünïcode video (final) #2.mov", "webm").First());
    }

    [Fact]
    public void LeadingDotOnExtension_IsNormalized()
    {
        Assert.Equal("a.discord.png", OutputNamer.CandidateFileNames("a.bmp", ".png").First());
    }
}
