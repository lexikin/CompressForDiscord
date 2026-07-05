using System;

namespace CompressForDiscord.Infrastructure;

/// <summary>Base for failures that map to a friendly dialog + process exit code.</summary>
internal class AppException(string message, int exitCode, string? details = null) : Exception(message)
{
    public int ExitCode { get; } = exitCode;

    /// <summary>Technical details (e.g. ffmpeg stderr tail) for the expandable section.</summary>
    public string? Details { get; } = details;
}

internal sealed class FfmpegNotFoundException(string message)
    : AppException(message, ExitCodes.FfmpegNotFound);

internal sealed class UnsupportedInputException(string message, string? details = null)
    : AppException(message, ExitCodes.UnsupportedInput, details);

internal sealed class CannotFitException(string message)
    : AppException(message, ExitCodes.CannotFit);

internal sealed class CompressionFailedException(string message, string? details = null)
    : AppException(message, ExitCodes.UnexpectedError, details);
