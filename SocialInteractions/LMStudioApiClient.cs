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
    public class LMStudioApiMessage
    {
        [DataMember(Name = "role")]
        public string Role { get; set; }
        [DataMember(Name = "content")]
        public string Content { get; set; }
    }

    [DataContract]
    public class LMStudioApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "messages")]
        public List<LMStudioApiMessage> Messages { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "max_tokens")]
        public int? MaxTokens { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }

        public LMStudioApiRequest()
        {
            Stream = false;
            Messages = new List<LMStudioApiMessage>();
        }
    }

    [DataContract]
    public class LMStudioApiChoice
    {
        [DataMember(Name = "index")]
        public int Index { get; set; }
        [DataMember(Name = "message")]
        public LMStudioApiMessage Message { get; set; }
        [DataMember(Name = "finish_reason")]
        public string FinishReason { get; set; }
    }

    [DataContract]
    public class LMStudioApiResponse
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }
        [DataMember(Name = "object")]
        public string Object { get; set; }
        [DataMember(Name = "created")]
        public long Created { get; set; }
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "choices")]
        public LMStudioApiChoice[] Choices { get; set; }
        [DataMember(Name = "usage")]
        public object Usage { get; set; } // We won't use this, but it's in the API response
    }

    public class LMStudioApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _modelName;
        private bool _disposed = false;

        public LMStudioApiClient(string apiUrl, string modelName)
        {
            _apiUrl = apiUrl;
            _modelName = modelName;
            _httpClient = SharedHttpClient;
        }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("LMStudioApiClient");

            try
            {
                var request = new LMStudioApiRequest
                {
                    Model = _modelName,
                    Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                    MaxTokens = maxLength,
                    Stream = false
                };

                // Add the prompt as a user message
                request.Messages.Add(new LMStudioApiMessage
                {
                    Role = "user",
                    Content = prompt
                });

                // Convert to JSON
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(LMStudioApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                // Log the request for debugging
                SLog.Message(string.Format("[SocialInteractions] LMStudio API Request: {0}", jsonContent));

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiUrl.TrimEnd('/') + "/v1/chat/completions", httpContent);
                
                // Log the response status code for debugging
                SLog.Message(string.Format("[SocialInteractions] LMStudio API Response Status: {0}", response.StatusCode));
                
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Log the response body for debugging
                SLog.Message(string.Format("[SocialInteractions] LMStudio API Response Body: {0}", responseBody));

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(LMStudioApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                LMStudioApiResponse apiResponse = (LMStudioApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && apiResponse.Choices != null && apiResponse.Choices.Length > 0)
                {
                    // Log the API request and response
                    // SLog.Message(string.Format("[SocialInteractions] LLM API Request: {0}", prompt));
                    // SLog.Message(string.Format("[SocialInteractions] LLM API Response: {0}", apiResponse.Choices[0].Message.Content));

                    return apiResponse.Choices[0].Message.Content;
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] LMStudioApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] LMStudioApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
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