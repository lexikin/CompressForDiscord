using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CompressForDiscord.Infrastructure;

namespace CompressForDiscord.ViewModels;

internal sealed class ErrorDialogViewModel(string headline, string message, string? details) : ObservableObject
{
    public string Headline { get; } = headline;
    public string Message { get; } = message;
    public string? Details { get; } = details;
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public static ErrorDialogViewModel FromException(Exception exception) => exception switch
    {
        FfmpegNotFoundException e => new("FFmpeg is missing", e.Message, e.Details),
        UnsupportedInputException e => new("Couldn't read this file", e.Message, e.Details),
        CannotFitException e => new("Can't fit under the limit", e.Message, e.Details),
        CompressionFailedException e => new("Compression failed", e.Message, e.Details),
        AppException e => new("Something went wrong", e.Message, e.Details),
        _ => new("Something went wrong", exception.Message, exception.ToString()),
    };
}
