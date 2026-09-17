using Microsoft.AspNetCore.Http;

namespace server.Services;

public interface IFileStorageService
{
    Task<string> SaveVideoAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<string> SaveAudioAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    void Delete(string? filePath);
}

public sealed class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;

    private const long MaxVideoSize = 500L * 1024 * 1024;
    private const long MaxAudioSize = 200L * 1024 * 1024;

    private static readonly Dictionary<string, string[]> VideoMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] =
            [
                "video/mp4"
            ],

            [".mov"] =
            [
                "video/quicktime"
            ],

            [".avi"] =
            [
                "video/x-msvideo",
                "video/avi"
            ],

            [".mkv"] =
            [
                "video/x-matroska"
            ],

            [".webm"] =
            [
                "video/webm"
            ]
        };

    private static readonly Dictionary<string, string[]> AudioMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".wav"] =
            [
                "audio/wav",
                "audio/x-wav",
                "audio/wave"
            ],

            [".mp3"] =
            [
                "audio/mpeg"
            ],

            [".m4a"] =
            [
                "audio/mp4",
                "audio/x-m4a"
            ],

            [".aac"] =
            [
                "audio/aac",
                "audio/x-aac"
            ],

            [".ogg"] =
            [
                "audio/ogg"
            ],

            [".flac"] =
            [
                "audio/flac",
                "audio/x-flac"
            ]
        };

    public FileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<string> SaveVideoAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            file,
            VideoMimeTypes,
            MaxVideoSize,
            "videos",
            cancellationToken);
    }

    public Task<string> SaveAudioAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            file,
            AudioMimeTypes,
            MaxAudioSize,
            "audio",
            cancellationToken);
    }

    private async Task<string> SaveAsync(
        IFormFile file,
        Dictionary<string, string[]> allowedTypes,
        long maxSize,
        string folder,
        CancellationToken cancellationToken)
    {
        ValidateBasicFile(file, maxSize);

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        if (!allowedTypes.ContainsKey(extension))
        {
            throw new ArgumentException(
                "File type is not supported.");
        }

        if (!allowedTypes[extension]
            .Contains(
                file.ContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Invalid content type.");
        }

        // IMPORTANT:
        // Keep uploaded files OUTSIDE wwwroot.
        var rootPath = Path.Combine(
            _environment.ContentRootPath,
            "Storage");

        var directory = Path.Combine(
            rootPath,
            folder);

        Directory.CreateDirectory(directory);

        // Never use the original filename.
        var generatedFileName =
            $"{Guid.NewGuid():N}{extension}";

        var fullPath = Path.Combine(
            directory,
            generatedFileName);

        // Extra path safety check.
        var fullDirectoryPath =
            Path.GetFullPath(directory);

        var fullFilePath =
            Path.GetFullPath(fullPath);

        if (!fullFilePath.StartsWith(
                fullDirectoryPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "Invalid file path.");
        }

        try
        {
            await using var stream =
                new FileStream(
                    fullFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    options: FileOptions.Asynchronous |
                             FileOptions.SequentialScan);

            await file.CopyToAsync(
                stream,
                cancellationToken);

            return fullFilePath;
        }
        catch
        {
            // Remove partially uploaded file.
            Delete(fullFilePath);

            throw;
        }
    }

    private static void ValidateBasicFile(
        IFormFile file,
        long maxSize)
    {
        if (file == null)
        {
            throw new ArgumentException(
                "File is required.");
        }

        if (file.Length <= 0)
        {
            throw new ArgumentException(
                "File is empty.");
        }

        if (file.Length > maxSize)
        {
            throw new ArgumentException(
                "File exceeds the maximum allowed size.");
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            throw new ArgumentException(
                "Invalid file name.");
        }
    }

    public void Delete(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Don't allow cleanup failure
            // to hide the original operation.
        }
    }
}