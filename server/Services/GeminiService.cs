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
                Attempts = 3,
                InitialDelay = 2.0,
                ExpBase = 2.0,
                MaxDelay = 30.0,
                Jitter = 1.0,
                HttpStatusCodes = new List<int> { 408, 429, 500, 502, 503, 504 }
            };

            _client = new Client(
                apiKey: apiKey,
                httpOptions: new HttpOptions { RetryOptions = retryOptions }
            );
        }

        public async Task<string> GenerateAsync(string transcript)
        {
            var prompt = $$"""
You are an expert educational content generator.

Analyze the following video transcript and create clear, accurate,
structured educational content.

The transcript was generated using speech-to-text and may contain
minor transcription errors, especially with technical terms,
names, abbreviations, or words that sound similar.

Correct obvious transcription errors using the context of the
transcript and your general knowledge.

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

1. Title — concise educational title describing the main topic.

2. Summary — clear, concise summary focused on the main concepts.

3. Learning Objectives — 3 to 5 objectives directly supported by
   the transcript.

4. Important Points — EXACTLY 5 important concepts or facts.

5. Multiple Choice Questions — EXACTLY 5 MCQs, each with:
   - EXACTLY 4 options
   - exactly ONE correct answer
   - an explanation
   - a difficulty: easy, medium, or hard

Output requirements:
- Return valid JSON only.
- Do NOT wrap the JSON in markdown code fences.
- Do NOT add commentary before or after the JSON.
- Do not add extra fields.

The JSON must match this EXACT schema:

{
  "title": "string",
  "summary": "string",
  "learning_objectives": ["string", "string", "string"],
  "important_points": ["string", "string", "string", "string", "string"],
  "multiple_choice_questions": [
    {
      "question": "string",
      "options": ["string", "string", "string", "string"],
      "correct_answer": "string",
      "explanation": "string",
      "difficulty": "easy"
    }
  ]
}

CRITICAL RULES for multiple_choice_questions:
- "correct_answer" MUST be the EXACT full text of one of the
  strings in "options".
- Do NOT use letters like "A", "B", "C", "D".
- Do NOT use an index number like 0, 1, 2, 3.
- The text must match character-for-character, including
  capitalization and punctuation.
- "difficulty" must be exactly one of: "easy", "medium", "hard".

Video Transcript:
{{transcript}}
""";

            var config = new GenerateContentConfig
            {
                ResponseMimeType = "application/json",
                ThinkingConfig = new ThinkingConfig
                {
                    ThinkingLevel = "minimal"
                }
            };

            var modelsToTry = new[]
            {
                "gemini-3.6-flash",
                "gemini-3.1-flash-lite"
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

                    return CleanJson(response.Text!);
                }
                catch (Exception ex) when (
                    ex.Message.Contains("high demand") ||
                    ex.Message.Contains("503") ||
                    ex.Message.Contains("UNAVAILABLE"))
                {
                    lastException = ex;
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            throw new Exception(
                "All Gemini models are temporarily unavailable. Please try again later.",
                lastException);
        }

        /// <summary>
        /// Strips markdown fences and stray text so the response is pure JSON.
        /// </summary>
        private static string CleanJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new Exception("Gemini returned an empty response.");

            var text = raw.Trim();

            // Strip ```json ... ``` or ``` ... ``` fences
            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                    text = text[(firstNewline + 1)..];

                int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                    text = text[..lastFence];

                text = text.Trim();
            }

            // Extract outermost { ... } if stray text remains
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text[start..(end + 1)];

            return text;
        }
    }
}