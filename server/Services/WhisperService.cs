using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

public class WhisperService : IDisposable
{
    private readonly string _modelPath;
    private WhisperFactory? _whisperFactory;

    public WhisperService()
    {
        var modelsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "models"
        );

        Directory.CreateDirectory(modelsDirectory);

        _modelPath = Path.Combine(
            modelsDirectory,
            "ggml-tiny.bin"
        );
    }

    public async Task<string> TranscribeAsync(
        string audioPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioPath))
        {
            throw new FileNotFoundException(
                "Audio file not found.",
                audioPath
            );
        }

        await EnsureModelExistsAsync(cancellationToken);

        _whisperFactory ??=
            WhisperFactory.FromPath(_modelPath);

        using var processor = _whisperFactory
            .CreateBuilder()
            .WithLanguage("auto")
            .Build();

        await using var audioStream =
            File.OpenRead(audioPath);

        var transcription = new StringBuilder();

        await foreach (
            var segment in processor.ProcessAsync(
                audioStream,
                cancellationToken))
        {
            transcription.Append(segment.Text);
        }

        return transcription
            .ToString()
            .Trim();
    }

    private async Task EnsureModelExistsAsync(
        CancellationToken cancellationToken)
    {
        if (File.Exists(_modelPath))
        {
            return;
        }

        Console.WriteLine("Whisper Tiny model not found.");
        Console.WriteLine("Downloading model...");

        using var modelStream =
            await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(
                    GgmlType.Tiny,
                    cancellationToken: cancellationToken);

        await using var fileStream =
            File.Create(_modelPath);

        await modelStream.CopyToAsync(
            fileStream,
            cancellationToken);

        Console.WriteLine("Whisper model downloaded.");
    }

    public void Dispose()
    {
        _whisperFactory?.Dispose();
    }
}