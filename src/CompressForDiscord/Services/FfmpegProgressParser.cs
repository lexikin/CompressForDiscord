using System;
using System.Globalization;

namespace CompressForDiscord.Services;

/// <summary>One `-progress` block worth of state.</summary>
internal sealed record ProgressUpdate(double Fraction, double? Speed, bool Ended);

/// <summary>
/// Parses `ffmpeg -progress pipe:1` key/value lines. ffmpeg emits blocks of
/// key=value pairs terminated by a `progress=continue|end` line; one
/// <see cref="ProgressUpdate"/> is produced per block.
/// </summary>
internal sealed class FfmpegProgressParser
{
    private readonly double? _durationSeconds;

    private double _outTimeSeconds;
    private double? _speed;
    private bool _blockHasMicroseconds;

    public FfmpegProgressParser(double? durationSeconds) => _durationSeconds = durationSeconds;

    /// <summary>Feed one stdout line; returns an update when the line completes a block.</summary>
    public ProgressUpdate? ParseLine(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        int eq = line.IndexOf('=');
        if (eq <= 0)
        {
            return null;
        }

        string key = line[..eq];
        string value = line[(eq + 1)..].Trim();

        switch (key)
        {
            case "out_time_us":
                if (long.TryParse(value, CultureInfo.InvariantCulture, out long us))
                {
                    _outTimeSeconds = us / 1_000_000.0;
                    _blockHasMicroseconds = true;
                }

                break;

            case "out_time_ms":
                // Long-standing ffmpeg quirk: out_time_ms is microseconds, not milliseconds.
                // Only used when out_time_us was absent from this block.
                if (!_blockHasMicroseconds &&
                    long.TryParse(value, CultureInfo.InvariantCulture, out long notMs))
                {
                    _outTimeSeconds = notMs / 1_000_000.0;
                }

                break;

            case "out_time":
                if (!_blockHasMicroseconds &&
                    TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
                {
                    _outTimeSeconds = ts.TotalSeconds;
                }

                break;

            case "speed":
                _speed = ParseSpeed(value);
                break;

            case "progress":
                bool ended = value == "end";
                var update = new ProgressUpdate(ComputeFraction(ended), _speed, ended);
                _blockHasMicroseconds = false; // next block starts fresh
                return update;
        }

        return null;
    }

    private double ComputeFraction(bool ended)
    {
        if (ended)
        {
            return 1.0;
        }

        if (_durationSeconds is not (> 0 and var duration))
        {
            return 0.0; // unknown duration → caller shows an indeterminate bar
        }

        return Math.Clamp(_outTimeSeconds / duration, 0.0, 0.99);
    }

    private static double? ParseSpeed(string value)
    {
        // "12.3x" / "0.998x" / "N/A"
        if (value.EndsWith('x') &&
            double.TryParse(value.AsSpan(..^1), CultureInfo.InvariantCulture, out double speed) &&
            speed > 0)
        {
            return speed;
        }

        return null;
    }
}
