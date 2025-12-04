using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Collections.Generic;

namespace SocialInteractions
{
    [DataContract]
    public class GeminiApiPart
    {
        [DataMember(Name = "text", EmitDefaultValue = false)]
        public string Text { get; set; }
    }

    [DataContract]
    public class GeminiApiContent
    {
        [DataMember(Name = "parts")]
        public List<GeminiApiPart> Parts { get; set; }

        public GeminiApiContent()
        {
            Parts = new List<GeminiApiPart>();
        }
    }

    [DataContract]
    public class GeminiApiGenerationConfig
    {
        [DataMember(Name = "maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
        [DataMember(Name = "stopSequences")]
        public List<string> StopSequences { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
    }

    [DataContract]
    public class GeminiApiSystemInstruction
    {
        [DataMember(Name = "parts")]
        public List<GeminiApiPart> Parts { get; set; }

        public GeminiApiSystemInstruction()
        {
            Parts = new List<GeminiApiPart>();
        }
    }

    [DataContract]
    public class GeminiApiRequest
    {
        [DataMember(Name = "contents")]
        public List<GeminiApiContent> Contents { get; set; }
        [DataMember(Name = "generationConfig", EmitDefaultValue = false)]
        public GeminiApiGenerationConfig GenerationConfig { get; set; }
        [DataMember(Name = "systemInstruction", EmitDefaultValue = false)]
        public GeminiApiSystemInstruction SystemInstruction { get; set; }

        public GeminiApiRequest()
        {
            Contents = new List<GeminiApiContent>();
        }
    }

    [DataContract]
    public class GeminiApiResponseCandidate
    {
        [DataMember(Name = "content")]
        public GeminiApiContent Content { get; set; }
    }

    [DataContract]
    public class GeminiApiResponse
    {
        [DataMember(Name = "candidates")]
        public GeminiApiResponseCandidate[] Candidates { get; set; }
    }

    public class GeminiApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private bool _disposed = false;

        public GeminiApiClient(string apiUrl, string apiKey)
        {
            _apiUrl = apiUrl;
            // Trim whitespace which can cause header issues
            _apiKey = (apiKey != null) ? apiKey.Trim() : null;
            _httpClient = SharedHttpClient;
            
            // Clear any existing default request headers
            _httpClient.DefaultRequestHeaders.Clear();
            
            // Add required headers for Gemini API
            _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SocialInteractionsMod/1.0");
        }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("GeminiApiClient");

            try
            {
                var request = new GeminiApiRequest();
                
                // Add generation config
                request.GenerationConfig = new GeminiApiGenerationConfig
                {
                    MaxOutputTokens = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    StopSequences = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                };

                // Add system instruction
                request.SystemInstruction = new GeminiApiSystemInstruction();
                request.SystemInstruction.Parts.Add(new GeminiApiPart
                {
                    Text = "You are generating dialogue for characters in a story. Respond with only the dialogue lines, without any thinking, reasoning, or meta-commentary. Do not include tags like <thinking> or explanations."
                });
                
                // Add the prompt as a user message content part
                var content = new GeminiApiContent();
                content.Parts.Add(new GeminiApiPart { Text = prompt });
                request.Contents.Add(content);

                // Convert to JSON
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(GeminiApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                // Log the request for debugging
                SLog.Message(string.Format("[SocialInteractions] Gemini API Request: {0}", jsonContent));

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Construct the Gemini API endpoint URL based on the model name from settings
                string geminiModel = SocialInteractions.Settings.geminiModelName;
                if (string.IsNullOrEmpty(geminiModel))
                {
                    geminiModel = "gemini-2.5-flash";  // Default model
                }
                
                string fullUrl = string.Format("{0}/v1beta/models/{1}:generateContent", _apiUrl.TrimEnd('/'), geminiModel);

                var response = await _httpClient.PostAsync(fullUrl, httpContent);
                
                // Log the response status code for debugging
                SLog.Message(string.Format("[SocialInteractions] Gemini API Response Status: {0}", response.StatusCode));
                
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Log the response body for debugging
                SLog.Message(string.Format("[SocialInteractions] Gemini API Response Body: {0}", responseBody));

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(GeminiApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                GeminiApiResponse apiResponse = (GeminiApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && apiResponse.Candidates != null && apiResponse.Candidates.Length > 0)
                {
                    // Extract the text from the first candidate's content
                    var candidate = apiResponse.Candidates[0];
                    if (candidate.Content != null && candidate.Content.Parts != null && candidate.Content.Parts.Count > 0)
                    {
                        return CleanChatResponse(candidate.Content.Parts[0].Text);
                    }
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] GeminiApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] GeminiApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
        }

        private string CleanChatResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            // Remove thinking blocks with various tag formats
            response = System.Text.RegularExpressions.Regex.Replace(response, @"<thinking>.*?</thinking>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            response = System.Text.RegularExpressions.Regex.Replace(response, @"\[thinking\].*?\[/thinking\]", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Trim whitespace
            response = response.Trim();
            
            return response;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // We don't dispose the shared HttpClient as it's shared
                    // Only dispose if we had a custom HttpClient
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}