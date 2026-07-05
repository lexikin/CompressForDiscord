using System;
using System.Threading;
using System.Threading.Tasks;
using CompressForDiscord.Models;

namespace CompressForDiscord.Services;

/// <summary>What a compressor produced, still inside the job temp dir.</summary>
internal sealed record CompressorOutput(string Path, int Attempts, int? Width, int? Height);

internal interface IMediaCompressor
{
    /// <summary>
    /// Compresses <paramref name="media"/> to fit <paramref name="limitBytes"/>, writing all
    /// intermediates and the final file into <paramref name="jobTempDir"/>. The orchestrator
    /// moves the produced file into place afterwards.
    /// </summary>
    Task<CompressorOutput> CompressAsync(
        MediaInfo media,
        long limitBytes,
        string jobTempDir,
        IProgress<CompressionProgress> progress,
        CancellationToken ct);
}
