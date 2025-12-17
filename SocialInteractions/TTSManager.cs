using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Text;

namespace SocialInteractions
{
    public static class TTSManager
    {
        // Managed AudioSource
        private static AudioSource audioSource;


        public static void Initialize()
        {
            // No initialization needed for API currently
        }

        public static void Speak(string text, Pawn speaker, float speed = 1.0f, int volume = 100)
        {
            if (!SocialInteractions.Settings.enableTTS || SocialInteractions.Settings.ttsMuted) return;

            string voiceName = "alloy";
            if (speaker != null && Current.Game != null)
            {
                var manager = Current.Game.GetComponent<VoiceAssignmentManager>();
                if (manager != null)
                {
                    voiceName = manager.GetOrAssignVoice(speaker);
                }
            }

            // In the new system, we only use External API
            SpeakWithApi(text, voiceName);
        }

        public static void Stop()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        public static List<string> GetVoices()
        {
             return VoiceAssignmentManager.AvailableVoices;
        }

        public static void FetchVoicesFromApi()
        {
            if (string.IsNullOrEmpty(SocialInteractions.Settings.ttsApiUrl)) return;
            
            // Try to deduce the voices endpoint
            string speechUrl = SocialInteractions.Settings.ttsApiUrl;
            string voicesUrl = speechUrl.Replace("/speech", "/voices"); // Simple heuristic
            
            if (voicesUrl == speechUrl) voicesUrl = speechUrl + ((speechUrl.EndsWith("/")) ? "" : "/") + "v1/audio/voices"; // Fallback

            SLog.Message("[SocialInteractions] Fetching voices from: " + voicesUrl);

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                ((MonoBehaviour)Current.Root).StartCoroutine(FetchVoicesCoroutine(voicesUrl));
            });
        }

        private static IEnumerator FetchVoicesCoroutine(string url)
        {
             string apiKey = SocialInteractions.Settings.ttsApiKey;
             var request = UnityWebRequest.Get(url);
             if (!string.IsNullOrEmpty(apiKey))
             {
                 request.SetRequestHeader("Authorization", "Bearer " + apiKey);
             }

             yield return request.SendWebRequest();

             if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
             {
                 SLog.Error("[SocialInteractions] Failed to fetch voices: " + request.error);
             }
             else
             {
                 string json = request.downloadHandler.text;
                 SLog.Message("[SocialInteractions] Voices response: " + json);

                 List<string> voices = new List<string>();

                 try
                 {
                     // Simple parsing without JsonUtility dependency issues
                     // Look for "voices":[ ... ] block (for OpenAI-compatible APIs)
                     int voicesStart = json.IndexOf("\"voices\"");
                     if (voicesStart != -1)
                     {
                         int arrayStart = json.IndexOf('[', voicesStart);
                         int arrayEnd = json.IndexOf(']', arrayStart);
                         if (arrayStart != -1 && arrayEnd != -1)
                         {
                             string arrayContent = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
                             ParseVoiceArray(arrayContent, ref voices);
                         }
                     }
                     else
                     {
                         // If "voices" not found, try parsing as direct array [ "voice1", "voice2", ... ]
                         int arrayStart = json.IndexOf('[');
                         int arrayEnd = json.LastIndexOf(']');
                         if (arrayStart != -1 && arrayEnd != -1 && arrayEnd > arrayStart)
                         {
                             string arrayContent = json.Substring(arrayStart, arrayEnd - arrayStart + 1);
                             ParseVoiceArray(arrayContent, ref voices);
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                     SLog.Warning("[SocialInteractions] Parsing failed: " + ex.Message);
                 }

                 if (voices.Count > 0)
                 {
                     SLog.Message(string.Format("[SocialInteractions] Found {0} voices.", voices.Count));
                     VoiceAssignmentManager.SetAvailableVoices(voices);
                 }
                 else
                 {
                     SLog.Warning("[SocialInteractions] No voices found in response.");
                 }
             }
        }

        private static void ParseVoiceArray(string arrayContent, ref List<string> voices)
        {
            // Find all strings like "abc_123" or "abc_123.wav" inside the array
            var matches = System.Text.RegularExpressions.Regex.Matches(arrayContent, "\"([a-zA-Z0-9_.]+)\"");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string v = match.Groups[1].Value;

                if (!voices.Contains(v)) voices.Add(v);
            }
        }

        private static void SpeakWithApi(string text, string voiceName)
        {
            if (string.IsNullOrEmpty(SocialInteractions.Settings.ttsApiUrl))
            {
                SLog.Warning("[SocialInteractions] TTS API URL is empty.");
                return;
            }

            // Use LongEventHandler to run on the main thread
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                // Current.Root is a MonoBehaviour, so we can use it to start coroutines
                ((MonoBehaviour)Current.Root).StartCoroutine(FetchAndPlayAudio(text, voiceName));
            });
        }

        private static IEnumerator FetchAndPlayAudio(string text, string voiceName)
        {
            string url = SocialInteractions.Settings.ttsApiUrl;
            string apiKey = SocialInteractions.Settings.ttsApiKey;
            string model = SocialInteractions.Settings.ttsModel;

            // Use provided voice name, or fallback to "alloy" (OpenAI default) or whatever is set in settings (though settings voice is removed)
            // We will refine this when we implement the VoiceAssignmentManager
            string voice = !string.IsNullOrEmpty(voiceName) ? voiceName : "alloy";

            // Create JSON payload
            // Use speed directly from settings
            float speed = SocialInteractions.Settings.ttsSpeed;
            
            // Clamp strictly to OpenAI bounds (0.25 to 4.0) just in case
            speed = Mathf.Clamp(speed, 0.25f, 4.0f);

            // Create JSON payload with speed
            string json = string.Format("{{\"model\": \"{0}\", \"input\": \"{1}\", \"voice\": \"{2}\", \"speed\": {3}}}", 
                model, 
                text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", ""), 
                voice, 
                speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            
            // Create a PUT request and change to POST (the original approach from the working code)
            var request = UnityWebRequest.Put(url, json);
            request.method = "POST";  // Change to POST method after creation, but keep it first try as WAV
            request.SetRequestHeader("Content-Type", "application/json");

            // Use default audio type (WAV) for initial request
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);

            if (!string.IsNullOrEmpty(apiKey))
            {
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                SLog.Error("[SocialInteractions] TTS API Error: " + request.error + "\n" + request.downloadHandler.text);
            }
            else
            {
                // Try to get the audio clip with WAV format first
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);

                // If it failed with WAV, try with other formats
                if (clip == null || clip.frequency == 0)  // Check if clip has no valid data
                {
                    SLog.Message("[SocialInteractions] WAV format failed, trying MPEG format...");

                    // Retry with MPEG format
                    var mpegRequest = UnityWebRequest.Put(url, json);
                    mpegRequest.method = "POST";
                    mpegRequest.SetRequestHeader("Content-Type", "application/json");
                    mpegRequest.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        mpegRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);
                    }

                    yield return mpegRequest.SendWebRequest();

                    if (mpegRequest.result == UnityWebRequest.Result.ConnectionError || mpegRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        SLog.Error("[SocialInteractions] TTS API MPEG Error: " + mpegRequest.error);
                    }
                    else
                    {
                        clip = DownloadHandlerAudioClip.GetContent(mpegRequest);
                        // Check if the clip has valid data (frequency > 0)
                        if (clip != null && clip.frequency == 0)
                        {
                            SLog.Message("[SocialInteractions] MPEG format also invalid, clip has 0 frequency, trying OGG...");

                            // Final fallback to OGG
                            var oggRequest = UnityWebRequest.Put(url, json);
                            oggRequest.method = "POST";
                            oggRequest.SetRequestHeader("Content-Type", "application/json");
                            oggRequest.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.OGGVORBIS);

                            if (!string.IsNullOrEmpty(apiKey))
                            {
                                oggRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);
                            }

                            yield return oggRequest.SendWebRequest();

                            if (oggRequest.result == UnityWebRequest.Result.ConnectionError || oggRequest.result == UnityWebRequest.Result.ProtocolError)
                            {
                                SLog.Error("[SocialInteractions] TTS API OGG Error: " + oggRequest.error);
                            }
                            else
                            {
                                clip = DownloadHandlerAudioClip.GetContent(oggRequest);
                            }
                        }
                    }
                }

                if (clip == null)
                {
                    SLog.Warning("[SocialInteractions] Failed to get audio clip from TTS response with any format. Server may be returning incompatible audio format.");
                }
                else
                {
                    SLog.Message(string.Format("[SocialInteractions] Audio clip loaded: {0}Hz, {1} seconds, {2} channels", clip.frequency, clip.length, clip.channels));

                    // Ensure audio source is created and configured properly
                    if (audioSource == null)
                    {
                        GameObject go = new GameObject("SocialInteractions_TTS_Audio");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        audioSource = go.AddComponent<AudioSource>();
                        audioSource.playOnAwake = false; // Don't play automatically
                        audioSource.spatialBlend = 0f; // 2D sound (0 = fully 2D, 1 = fully 3D)
                    }

                    // Ensure audio source is configured properly
                    audioSource.spatialBlend = 0f; // Ensure 2D sound
                    audioSource.rolloffMode = AudioRolloffMode.Logarithmic; // Set rolloff mode
                    audioSource.maxDistance = 500f; // Set a reasonable max distance
                    audioSource.minDistance = 1f; // Set min distance

                    // Apply volume from settings (0-100 to 0-1 range)
                    float vol = SocialInteractions.Settings.ttsVolume / 100f;
                    SLog.Message(string.Format("[SocialInteractions] Playing audio clip with volume: {0}", vol));

                    // Try playing the clip with the configured volume
                    audioSource.PlayOneShot(clip, vol);
                    SLog.Message("[SocialInteractions] Successfully played TTS audio clip.");
                }
            }
        }
    }
}
