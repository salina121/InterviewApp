// Services/GeminiAgentService.cs
using System.Text;
using System.Text.Json;

namespace InterviewBot.Services
{
    public class GeminiAgentService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;

        public GeminiAgentService(IConfiguration config)
        {
            _httpClient = new HttpClient();
            _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey configuration is missing");
        }

        public async Task<string> AskQuestionAsync(string message)
        {
            var payload = new
            {
                contents = new[]
                {
                new
                {
                    parts = new[]
                    {
                        new { text = "You are a professional interviewer conducting a mock technical interview. " +
                                  "Your role is to ask clear, concise questions one at a time and evaluate answers. " +
                                  "Do not provide solutions or explanations unless asked to evaluate at the end. " +
                                  "Maintain a professional tone. " +
                                  "Here's the request: " + message }
                    }
                }
            },
                generationConfig = new
                {
                    temperature = 0.7,
                    topP = 0.9,
                    maxOutputTokens = 1024
                }
            };

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}"),
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync();
            var json = await JsonDocument.ParseAsync(contentStream);
            var result = json.RootElement
                             .GetProperty("candidates")[0]
                             .GetProperty("content")
                             .GetProperty("parts")[0]
                             .GetProperty("text")
                             .GetString();

            return result ?? string.Empty;
        }


    }
}