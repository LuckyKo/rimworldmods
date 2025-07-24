using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Verse;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;

namespace SocialInteractions
{
    [DataContract]
    public class KoboldApiRequest
    {
        [DataMember(Name = "prompt")]
        public string Prompt { get; set; }
        [DataMember(Name = "max_length")]
        public int MaxLength { get; set; }
        [DataMember(Name = "temperature")]
        public float Temperature { get; set; }

        public KoboldApiRequest()
        {
            MaxLength = 200;
            Temperature = 0.7f;
        }
    }

    [DataContract]
    public class KoboldApiResponse
    {
        [DataMember(Name = "results")]
        public KoboldApiResult[] Results { get; set; }
    }

    [DataContract]
    public class KoboldApiResult
    {
        [DataMember(Name = "text")]
        public string Text { get; set; }
    }

    public class KoboldApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;

        public KoboldApiClient(string apiUrl, string apiKey)
        {
            _apiUrl = apiUrl;
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            // Add default request headers if needed, e.g., for API key
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", _apiKey));
            }
        }

        public async Task<string> GenerateText(string prompt, int maxLength, float temperature)
        {
            try
            {
                var request = new KoboldApiRequest
                {
                    Prompt = prompt,
                    MaxLength = maxLength,
                    Temperature = temperature
                };

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(KoboldApiRequest));
                MemoryStream stream = new MemoryStream();
                serializer.WriteObject(stream, request);
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                string jsonContent = reader.ReadToEnd();

                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiUrl + "/api/v1/generate", httpContent);
                response.EnsureSuccessStatusCode(); // Throws an exception if the HTTP response status is an error code

                var responseBody = await response.Content.ReadAsStringAsync();

                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(KoboldApiResponse));
                MemoryStream responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
                KoboldApiResponse apiResponse = (KoboldApiResponse)deserializer.ReadObject(responseStream);

                if (apiResponse != null && apiResponse.Results != null && apiResponse.Results.Length > 0)
                {
                    return apiResponse.Results[0].Text;
                }
                return null;
            }
            catch (HttpRequestException e)
            {
                return null;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
