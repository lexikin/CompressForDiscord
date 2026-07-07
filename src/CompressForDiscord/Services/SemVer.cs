using System;
using System.Globalization;

namespace CompressForDiscord.Services;

/// <summary>
/// Minimal SemVer 2.0 precedence comparison — enough to answer "is the release on GitHub newer
/// than the one running?". Build metadata (after '+') is ignored; a version with no prerelease
/// tag outranks the same core version with one (1.0.0 &gt; 1.0.0-rc.1).
/// </summary>
internal static class SemVer
{
    /// <summary>&gt;0 if <paramref name="a"/> is newer than <paramref name="b"/>, &lt;0 if older, 0 if equal.</summary>
    public static int Compare(string a, string b)
    {
        var (aCore, aPre) = Split(a);
        var (bCore, bPre) = Split(b);

        for (int i = 0; i < 3; i++)
        {
            int c = aCore[i].CompareTo(bCore[i]);
            if (c != 0)
            {
                return Math.Sign(c);
            }
        }

        if (aPre.Length == 0 && bPre.Length == 0)
        {
            return 0;
        }

        // A release (no prerelease tag) has higher precedence than a prerelease.
        if (aPre.Length == 0)
        {
            return 1;
        }

        if (bPre.Length == 0)
        {
            return -1;
        }

        return ComparePrerelease(aPre, bPre);
    }

    private static (int[] Core, string[] Pre) Split(string version)
    {
        string v = version.Trim();
        int plus = v.IndexOf('+');
        if (plus >= 0)
        {
            v = v[..plus];
        }

        int dash = v.IndexOf('-');
        string core = dash >= 0 ? v[..dash] : v;
        string pre = dash >= 0 ? v[(dash + 1)..] : "";

        var coreParts = core.Split('.');
        var nums = new int[3];
        for (int i = 0; i < 3; i++)
        {
            nums[i] = i < coreParts.Length
                && int.TryParse(coreParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                ? n : 0;
        }

        return (nums, pre.Length == 0 ? [] : pre.Split('.'));
    }

    private static int ComparePrerelease(string[] a, string[] b)
    {
        int shared = Math.Min(a.Length, b.Length);
        for (int i = 0; i < shared; i++)
        {
            bool aNum = int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ai);
            bool bNum = int.TryParse(b[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bi);

            int c = (aNum, bNum) switch
            {
                (true, true) => ai.CompareTo(bi),
                (true, false) => -1, // numeric identifiers rank below alphanumeric ones
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };

            if (c != 0)
            {
                return Math.Sign(c);
            }
        }

        // All shared identifiers equal → more identifiers wins (1.0.0-rc.1.1 > 1.0.0-rc.1).
        return a.Length.CompareTo(b.Length);
    }
}
