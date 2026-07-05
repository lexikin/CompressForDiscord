using System;

namespace CompressForDiscord.Services.Clipboard;

/// <summary>Pure text/uri-list construction (unicode, spaces, and '#' must survive escaping).</summary>
internal static class UriListBuilder
{
    internal static string ToFileUri(string absolutePath) => new Uri(absolutePath).AbsoluteUri;

    /// <summary>RFC 2483: one URI per line, CRLF line ends.</summary>
    internal static string BuildUriList(string absolutePath) => ToFileUri(absolutePath) + "\r\n";
}
