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
    public class OllamaApiRequest
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "prompt")]
        public string Prompt { get; set; }
        [DataMember(Name = "stream")]
        public bool Stream { get; set; }
        [DataMember(Name = "options")]
        public OllamaApiOptions Options { get; set; }

        public OllamaApiRequest()
        {
            Stream = false;
        }
    }

    [DataContract]
    public class OllamaApiOptions
    {
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }
        [DataMember(Name = "top_k")]
        public int TopK { get; set; }
        [DataMember(Name = "top_p")]
        public float TopP { get; set; }
        [DataMember(Name = "num_predict")]
        public int NumPredict { get; set; }
        [DataMember(Name = "stop")]
        public List<string> Stop { get; set; }
        [DataMember(Name = "mirostat")]
        public int Mirostat { get; set; }
        [DataMember(Name = "mirostat_tau")]
        public float MirostatTau { get; set; }
        [DataMember(Name = "mirostat_eta")]
        public float MirostatEta { get; set; }

        public OllamaApiOptions()
        {
            Mirostat = 0;
            MirostatTau = 5.0f;
            MirostatEta = 0.1f;
        }
    }

    [DataContract]
    public class OllamaApiResponse
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }
        [DataMember(Name = "response")]
        public string Response { get; set; }
        [DataMember(Name = "done")]
        public bool Done { get; set; }
    }

    public class OllamaApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _modelName;
        private bool _disposed = false;

        public OllamaApiClient(string apiUrl, string modelName)
        {
            _apiUrl = apiUrl;
            _modelName = modelName;
            _httpClient = SharedHttpClient;
        }

        public async Task<string> GenerateText(string prompt, int? maxLength = null, float? temperature = null, List<string> stopSequence = null, bool? enableXtcSampling = null, int? topK = null, float? topP = null, float? minP = null)
        {
            if (_disposed)
                throw new ObjectDisposedException("OllamaApiClient");

            try
            {
                var request = new OllamaApiRequest
                {
                    Model = _modelName,
                    Prompt = prompt,
                    Stream = false,
                    Options = new OllamaApiOptions
                    {
                        Temperature = temperature ?? SocialInteractions.Settings.llmTemperature,
                        TopK = topK ?? SocialInteractions.Settings.llmTopK,
                        TopP = topP ?? SocialInteractions.Settings.llmTopP,
                        NumPredict = maxLength ?? SocialInteractions.Settings.llmMaxTokens,
                        Stop = stopSequence ?? new List<string>(SocialInteractions.Settings.llmStoppingStrings.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    }
                };

                // Convert to JSON
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(OllamaApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiUrl + "/api/generate", httpContent);
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(OllamaApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                OllamaApiResponse apiResponse = (OllamaApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Response))
                {
                    return apiResponse.Response;
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] OllamaApiClient: HTTP request failed: {0}", ex.Message));
                return null;
            }
            catch (Exception ex)
            {
                SLog.Warning(string.Format("[SocialInteractions] OllamaApiClient: Unexpected error during text generation: {0}", ex.Message));
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Note: We don't dispose the shared HttpClient as it's shared
                // In a more sophisticated implementation, we might use HttpClientFactory
                _disposed = true;
            }
        }
    }
}