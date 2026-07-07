using System.Reflection;

namespace CompressForDiscord.Infrastructure;

/// <summary>
/// The app's own version, read once from the assembly. CI stamps it from the git tag
/// (<c>-p:Version=x.y.z</c>); local builds fall back to the <c>0.1.0-dev</c> default in
/// Directory.Build.props.
/// </summary>
internal static class AppVersion
{
    /// <summary>Clean version, e.g. "0.1.0-rc.7" — any SourceLink "+&lt;sha&gt;" suffix stripped.</summary>
    public static string Display { get; } = Compute();

    /// <summary>Window title with the version tag, e.g. "Compress for Discord v0.1.0-rc.7".</summary>
    public static string WindowTitle { get; } = $"Compress for Discord v{Display}";

    private static string Compute()
    {
        string? informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.1.0-dev";
        }

        int plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
