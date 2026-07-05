using System;
using CompressForDiscord.Services.Planning;
using Xunit;

namespace CompressForDiscord.Tests.Unit;

public sealed class PngScaleSearchTests
{
    /// <summary>Drives the search with a fake "PNG size is proportional to pixels" encoder.</summary>
    private static (double? BestFit, int Encodes, bool MinScaleTooLarge) Run(
        long targetBytes, Func<double, long> sizeAtScale, double minScale = 0.05, int maxEncodes = 6)
    {
        var search = new PngScaleSearch(targetBytes, minScale, maxEncodes);
        int encodes = 0;
        while (search.NextScale is double scale)
        {
            encodes++;
            Assert.InRange(encodes, 1, maxEncodes); // termination guard
            search.Report(scale, sizeAtScale(scale));
        }

        return (search.BestFit, encodes, search.MinScaleTooLarge);
    }

    [Fact]
    public void NativeResolutionFits_StopsAfterOneEncode()
    {
        var (bestFit, encodes, _) = Run(1_000_000, _ => 900_000);

        Assert.Equal(1.0, bestFit);
        Assert.Equal(1, encodes);
    }

    [Fact]
    public void OversizedImage_ConvergesToAFittingScale()
    {
        // 4x too big at native — the true fit boundary is scale 0.5.
        const long baseSize = 4_000_000;
        const long target = 1_000_000;

        var (bestFit, encodes, tooLarge) = Run(target, s => (long)(baseSize * s * s));

        Assert.False(tooLarge);
        Assert.NotNull(bestFit);
        Assert.True(encodes <= 6);
        Assert.True(baseSize * bestFit!.Value * bestFit.Value <= target, "chosen scale must actually fit");
        Assert.InRange(bestFit.Value, 0.4, 0.51); // near the true boundary, never over
    }

    [Fact]
    public void MassivelyOversized_TriesTheFloorThenGivesUp()
    {
        var (bestFit, _, tooLarge) = Run(1_000, _ => 100_000_000, minScale: 0.3);

        Assert.Null(bestFit);
        Assert.True(tooLarge);
    }

    [Fact]
    public void FitAtTheFloor_IsAccepted()
    {
        // Only scales at/below 0.3 fit; the floor is 0.3.
        var (bestFit, _, tooLarge) = Run(90_000, s => (long)(1_000_000 * s * s), minScale: 0.3);

        Assert.False(tooLarge);
        Assert.Equal(0.3, bestFit!.Value, precision: 9);
    }

    [Fact]
    public void ReportingAMismatchedScale_Throws()
    {
        var search = new PngScaleSearch(1_000, minScale: 0.05);

        Assert.Throws<InvalidOperationException>(() => search.Report(0.42, 500));
    }

    [Fact]
    public void RespectsMaxEncodeBudget()
    {
        var (_, encodes, _) = Run(1, s => (long)(long.MaxValue / 4 * s * s), maxEncodes: 4);

        Assert.True(encodes <= 4);
    }
}
