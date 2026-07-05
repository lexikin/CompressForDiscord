namespace CompressForDiscord.Infrastructure;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int UnexpectedError = 1;
    public const int BadArguments = 2;
    public const int Cancelled = 3;
    public const int CannotFit = 4;
    public const int UnsupportedInput = 5;
    public const int FfmpegNotFound = 6;
}
