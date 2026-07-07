using System;
using CompressForDiscord.Services;
using CompressForDiscord.Services.Clipboard;
using Microsoft.Extensions.DependencyInjection;

namespace CompressForDiscord.Infrastructure;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services
            .AddSingleton<IFfmpegLocator, FfmpegLocator>()
            .AddSingleton<IFfmpegRunner, FfmpegRunner>()
            .AddSingleton<IMediaProber, MediaProber>()
            .AddSingleton<IVideoEncoderSelector, VideoEncoderSelector>()
            .AddSingleton<IVideoCompressor, VideoCompressor>()
            .AddSingleton<IImageCompressor, ImageCompressor>()
            .AddSingleton<ICompressionOrchestrator, CompressionOrchestrator>()
            .AddSingleton<ISettingsService, SettingsService>()
            .AddSingleton<IVlcService, VlcService>()
            .AddSingleton<IThumbnailService, ThumbnailService>();

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IClipboardFileService, WindowsClipboard>();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IClipboardFileService, LinuxClipboard>();
        }
        else
        {
            services.AddSingleton<IClipboardFileService, NullClipboard>();
        }

        return services;
    }
}
