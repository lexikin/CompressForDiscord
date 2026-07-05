using System.Threading.Tasks;

namespace CompressForDiscord.Services.Clipboard;

/// <summary>Unsupported platforms: always fails → the caller's text-URI fallback kicks in.</summary>
internal sealed class NullClipboard : IClipboardFileService
{
    public Task<ClipboardOutcome> CopyFileAsync(string absolutePath) =>
        Task.FromResult(ClipboardOutcome.Failed);
}
