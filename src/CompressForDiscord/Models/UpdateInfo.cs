namespace CompressForDiscord.Models;

/// <summary>A newer release than the one running, discovered by <c>IUpdateChecker</c>.</summary>
internal sealed record UpdateInfo(string Version, string ReleaseUrl);
