using System.Collections.Generic;
using System.IO;

namespace CompressForDiscord.Services.Planning;

/// <summary>
/// Pure name generation. The orchestrator claims the first candidate it can create
/// with FileMode.CreateNew (race-safe collision handling).
/// </summary>
internal static class OutputNamer
{
    /// <summary>
    /// Yields "video.discord.webm", "video.discord (2).webm", "video.discord (3).webm", …
    /// for input file name "video.mp4" and target extension "webm".
    /// </summary>
    internal static IEnumerable<string> CandidateFileNames(string inputFileName, string targetExtension)
    {
        string stem = Path.GetFileNameWithoutExtension(inputFileName);
        string ext = targetExtension.TrimStart('.');

        yield return $"{stem}.discord.{ext}";
        for (int i = 2; ; i++)
        {
            yield return $"{stem}.discord ({i}).{ext}";
        }
    }
}
