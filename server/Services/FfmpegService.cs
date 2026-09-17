using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace server.Services;

public sealed class FfmpegService
{
    private readonly string _ffmpegPath;

    public FfmpegService()
    {
        _ffmpegPath = Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg");
    }

    private async Task InitializeAsync()
    {
        Directory.CreateDirectory(_ffmpegPath);

        var ffmpegExecutable = Path.Combine(
            _ffmpegPath,
            OperatingSystem.IsWindows()
                ? "ffmpeg.exe"
                : "ffmpeg");

        if (!File.Exists(ffmpegExecutable))
        {
            await FFmpegDownloader.GetLatestVersion(
                FFmpegVersion.Official,
                _ffmpegPath);
        }

        FFmpeg.SetExecutablesPath(_ffmpegPath);
    }

    public async Task<string> ConvertVideoToAudioAsync(
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException(
                "Video file not found.",
                videoPath);
        }

        await InitializeAsync();

        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "VideoToText");

        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(
            outputDirectory,
            $"{Guid.NewGuid():N}.wav");

        var conversion = FFmpeg.Conversions.New();

        conversion.AddParameter(
            $"-i \"{videoPath}\"");

        conversion.AddParameter("-vn");
        conversion.AddParameter("-ac 1");
        conversion.AddParameter("-ar 16000");
        conversion.AddParameter("-c:a pcm_s16le");

        conversion.SetOutput(outputPath);

        await conversion.Start(cancellationToken);

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                "Audio conversion failed.");
        }

        return outputPath;
    }
}