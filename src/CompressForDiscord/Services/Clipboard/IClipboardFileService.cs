using System.Threading.Tasks;

namespace CompressForDiscord.Services.Clipboard;

internal enum ClipboardOutcome
{
    /// <summary>A real file reference is on the clipboard — Ctrl+V into Discord uploads it.</summary>
    FileCopied,

    /// <summary>Only a text URI could be placed (missing tooling); banner adapts.</summary>
    TextFallback,

    Failed,
}

internal interface IClipboardFileService
{
    /// <summary>Puts a file *reference* (not bitmap data) on the system clipboard.</summary>
    Task<ClipboardOutcome> CopyFileAsync(string absolutePath);
}
