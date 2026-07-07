using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Infrastructure;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services;

internal interface IUpdateChecker
{
    /// <summary>
    /// Returns the latest published GitHub release if it's newer than the running build, else null.
    /// Best-effort and non-throwing — a failed check (offline, rate-limited, …) just returns null.
    /// </summary>
    Task<UpdateInfo?> CheckAsync(CancellationToken ct);
}

/// <summary>
/// Checks the GitHub Releases API. Prereleases are included (the project ships rc builds), so the
/// newest published, non-draft release is compared against <see cref="AppVersion.Display"/>.
/// </summary>
internal sealed class UpdateChecker : IUpdateChecker
{
    private const string ReleasesApi =
        "https://api.github.com/repos/lexikin/CompressForDiscord/releases?per_page=10";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly HttpClient Http = CreateClient();

    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            await using var stream = await Http.GetStreamAsync(ReleasesApi, timeout.Token).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);

            string current = AppVersion.Display;
            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean())
                {
                    continue; // drafts aren't downloadable
                }

                if (!release.TryGetProperty("tag_name", out var tagEl) || tagEl.GetString() is not { } tag)
                {
                    continue;
                }

                // Releases come newest-first: the first real one decides it.
                string latest = tag.TrimStart('v');
                if (SemVer.Compare(latest, current) > 0)
                {
                    string url = release.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
                    return new UpdateInfo(latest, url);
                }

                return null;
            }
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            AppLog.Write($"update check skipped: {e.Message}");
        }

        return null;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient();
        // GitHub rejects requests without a User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CompressForDiscord");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }
}
