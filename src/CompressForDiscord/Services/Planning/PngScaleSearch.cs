using System;

namespace CompressForDiscord.Services.Planning;

/// <summary>
/// Pure search state machine for the PNG downscale loop. The caller encodes at
/// <see cref="NextScale"/>, feeds the resulting byte count into <see cref="Report"/>,
/// and repeats until <see cref="NextScale"/> is null. PNG size is roughly O(pixels),
/// so the first correction jumps to sqrt(target/actual) and then bisects.
/// </summary>
internal sealed class PngScaleSearch
{
    private readonly long _targetBytes;
    private readonly double _minScale;
    private readonly int _maxEncodes;

    private int _encodes;
    private double _lo;        // largest known-fitting scale
    private double _hi = 1.0;  // smallest known-overshooting scale
    private double? _next;

    public PngScaleSearch(long targetBytes, double minScale, int maxEncodes = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetBytes, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxEncodes, 0);
        _targetBytes = targetBytes;
        _minScale = Math.Clamp(minScale, 0.01, 1.0);
        _maxEncodes = maxEncodes;
        _next = 1.0; // always try native resolution first
    }

    /// <summary>Scale factor to encode next, or null when the search is finished.</summary>
    public double? NextScale => _next;

    /// <summary>Largest scale that produced a fitting file, if any.</summary>
    public double? BestFit { get; private set; }

    /// <summary>True when fitting would require scaling below the minimum (image would get too tiny).</summary>
    public bool MinScaleTooLarge { get; private set; }

    public void Report(double scale, long bytes)
    {
        if (_next is null || Math.Abs(scale - _next.Value) > 1e-9)
        {
            throw new InvalidOperationException($"Reported scale {scale} does not match the requested {_next}.");
        }

        _encodes++;

        if (bytes <= _targetBytes)
        {
            if (BestFit is null || scale > BestFit)
            {
                BestFit = scale;
            }

            _lo = scale;
        }
        else
        {
            _hi = Math.Min(_hi, scale);
        }

        _next = ChooseNext(scale, bytes);
    }

    private double? ChooseNext(double lastScale, long lastBytes)
    {
        if (BestFit is not null && Math.Abs(BestFit.Value - 1.0) < 1e-9)
        {
            return null; // native resolution fits — done
        }

        if (_encodes >= _maxEncodes)
        {
            return null;
        }

        if (BestFit is null)
        {
            // Still hunting for any fit: pixel-proportional jump with a 5 % cushion.
            double guess = lastScale * Math.Sqrt((double)_targetBytes / lastBytes) * 0.95;
            guess = Math.Min(guess, lastScale * 0.9); // always shrink meaningfully

            if (guess < _minScale)
            {
                if (Math.Abs(lastScale - _minScale) < 1e-9)
                {
                    MinScaleTooLarge = true; // already tried the floor and it overshot
                    return null;
                }

                return _minScale; // one last try at the floor
            }

            return guess;
        }

        // Have a fit and an overshoot: bisect until the bracket is tight.
        if (_hi - _lo < 0.04)
        {
            return null;
        }

        return (_lo + _hi) / 2;
    }
}
