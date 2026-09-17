using Microsoft.AspNetCore.Mvc;
using server.Services;

[ApiController]
[Route("api/[controller]")]
public class TranscriptionController : ControllerBase
{
    private readonly ILogger<TranscriptionController> _logger;
    private readonly IFileStorageService _fileStorageService;
    private readonly FfmpegService _ffmpegService;
    private readonly WhisperService _whisperService;
    private readonly GeminiService _geminiService;

    public TranscriptionController(
        IFileStorageService fileStorageService,
        FfmpegService ffmpegService,
        WhisperService whisperService,
        GeminiService geminiService,
        ILogger<TranscriptionController> logger)
    {
        _fileStorageService = fileStorageService;
        _ffmpegService = ffmpegService;
        _whisperService = whisperService;
        _geminiService = geminiService;
        _logger = logger;
    }

    [HttpPost("video")]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Video(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        string? videoPath = null;
        string? audioPath = null;

        try
        {
            // 1. Save uploaded video
            videoPath =
                await _fileStorageService.SaveVideoAsync(
                    file,
                    cancellationToken);

            // 2. Video → WAV
            audioPath =
                await _ffmpegService.ConvertVideoToAudioAsync(
                    videoPath,
                    cancellationToken);

            // 3. WAV → Text
            var text =
                await _whisperService.TranscribeAsync(
                    audioPath,
                    cancellationToken);

            // 4. Text → Educational Content
            var educationalContent =
                await _geminiService.GenerateAsync(
                    text
                    );

            return Ok(new
            {
                fileName = file.FileName,
                text,
                educationalContent
            });
        }
        finally
        {
            SafeDelete(videoPath); // Delete uploaded video
            SafeDelete(audioPath); // Delete converted WAV
        }
    }

    private void SafeDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            _fileStorageService.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cleanup failed for '{Path}'", path);
        }
    }
}