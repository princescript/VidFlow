using Google.GenAI;
using Google.GenAI.Types;

namespace server.Services
{
    public class GeminiService
    {
        private readonly Client _client;

        public GeminiService(IConfiguration configuration)
        {
            var apiKey = configuration["Gemini:ApiKey"];

            // Configure retry options for transient server errors (503, 429, etc.)
            var retryOptions = new HttpRetryOptions
            {
                Attempts = 3,                    // Try each model 3 times
                InitialDelay = 2.0,              // Start with 2 seconds
                ExpBase = 2.0,                   // Double the delay each time
                MaxDelay = 30.0,                 // Cap at 30 seconds
                Jitter = 1.0,                    // Add randomness
                HttpStatusCodes = new List<int> { 408, 429, 500, 502, 503, 504 }
            };

            _client = new Client(
                apiKey: apiKey,
                httpOptions: new HttpOptions { RetryOptions = retryOptions }
            );
        }

        public async Task<string> GenerateAsync(string transcript)
        {
            var prompt = $"""
You are an expert educational content generator.

Analyze the following video transcript and create clear, accurate,
structured educational content.

The transcript was generated using speech-to-text and may contain
minor transcription errors, especially with technical terms,
names, abbreviations, or words that sound similar.

Correct obvious transcription errors using the context of the
transcript and your general knowledge.

For example, if a React transcript says "dipping" when the context
clearly refers to comparing the Virtual DOM with the previous
Virtual DOM, interpret it as "diffing".

Important rules for correcting transcription errors:
- Preserve the speaker's intended meaning.
- Correct obvious speech-to-text mistakes.
- Pay special attention to technical terminology.
- Do not introduce new concepts just because you know them.
- Do not add information that is unrelated to the transcript.
- If a word is ambiguous and cannot be confidently corrected from
  context, preserve its original meaning rather than inventing one.

Use only the concepts and information presented or clearly implied
by the transcript after correcting obvious transcription errors.

Do not invent additional facts, concepts, examples, explanations,
or information that are unrelated to the transcript.

Generate:

1. Title
   - Create a concise educational title describing the main topic.
   - The title must accurately represent the content of the transcript.

2. Summary
   - Provide a clear and concise summary of the transcript.
   - Focus on the main concepts and ideas discussed by the speaker.
   - Correct obvious transcription errors when necessary.
   - Do not add unrelated information.

3. Learning Objectives
   - Generate 3 to 5 things a learner should understand after
     studying the transcript.
   - Objectives must be directly supported by the transcript.
   - Focus on understanding concepts rather than memorizing sentences.

4. Important Points
   - Generate EXACTLY 5 important concepts or facts from the transcript.
   - Each point must be directly supported by the transcript.
   - Correct obvious transcription errors when necessary.
   - Do not introduce unrelated concepts.

5. Multiple Choice Questions
   - Generate EXACTLY 5 MCQs.
   - Each question must have EXACTLY 4 options.
   - There must be exactly ONE correct answer.
   - Include an explanation for the correct answer.
   - Assign each question a difficulty:
     easy, medium, or hard.
   - Questions should test understanding rather than simply copying
     sentences from the transcript.
   - All questions and answers must be based strictly on the transcript
     after correcting obvious transcription errors.
   - Do not create questions about concepts that were not discussed
     in the transcript.
   - Make sure the correct answer is actually supported by the transcript.
   - Do not use incorrect transcription terms as correct answers when
     the intended technical term can be confidently determined.

Output requirements:
- Follow the required response schema exactly.
- Return valid structured data according to the schema.
- Do not add extra fields.
- Do not add commentary outside the response schema.

Video Transcript:
{transcript}
""";

            var config = new GenerateContentConfig
            {
                ThinkingConfig = new ThinkingConfig
                {
                    ThinkingLevel = "minimal"
                }
            };

            // Fallback chain: try these models in order
            // Primary has best quality, fallback has higher free-tier quota
            var modelsToTry = new[]
            {
                "gemini-3.6-flash",      // Primary: best quality
                "gemini-3.1-flash-lite"  // Fallback: much higher RPD on free tier
            };

            Exception? lastException = null;

            foreach (var model in modelsToTry)
            {
                try
                {
                    var response = await _client.Models.GenerateContentAsync(
                        model: model,
                        contents: prompt,
                        config: config
                    );

                    if (string.IsNullOrEmpty(response.Text))
                    {
                        throw new Exception("Gemini returned an empty response.");
                    }

                    return response.Text;
                }
                catch (Exception ex) when (ex.Message.Contains("high demand") ||
                                           ex.Message.Contains("503") ||
                                           ex.Message.Contains("UNAVAILABLE"))
                {
                    lastException = ex;
                    // Brief pause before trying the next model
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            throw new Exception("All Gemini models are temporarily unavailable. Please try again later.", lastException);
        }
    }
}