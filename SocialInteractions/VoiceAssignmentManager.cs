using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace SocialInteractions
{
    public class VoiceAssignmentManager : GameComponent
    {
        // Persistent mapping of Pawn -> VoiceName
        private Dictionary<Pawn, string> voiceMapping = new Dictionary<Pawn, string>();

        // Cache for available voices (runtime only, not saved)
        private static List<string> availableVoices = new List<string>();
        
        public static List<string> AvailableVoices
        {
            get { return availableVoices; }
        }

        public VoiceAssignmentManager(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (SocialInteractions.Settings.enableTTS)
            {
                TTSManager.FetchVoicesFromApi();
            }
        }

        public void ResetAllocations()
        {
            if (voiceMapping != null)
            {
                voiceMapping.Clear();
                SLog.Message("[SocialInteractions] Voice allocations reset.");
            }
        }

        // Working lists for Scribe
        private List<Pawn> scribeKeys;
        private List<string> scribeValues;

        public override void ExposeData()
        {
            base.ExposeData();
            // Save/Load the dictionary. Keys are Pawns (References), Values are Strings (Voice Names).
            Scribe_Collections.Look(ref voiceMapping, "voiceMapping", LookMode.Reference, LookMode.Value, ref scribeKeys, ref scribeValues);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (voiceMapping == null)
                {
                    voiceMapping = new Dictionary<Pawn, string>();
                }
                else
                {
                    // Clean up null keys (pawns that no longer exist)
                    List<Pawn> nullKeys = new List<Pawn>();
                    foreach (var kvp in voiceMapping)
                    {
                        if (kvp.Key == null)
                        {
                            nullKeys.Add(kvp.Key);
                        }
                    }
                    
                    foreach (var nullKey in nullKeys)
                    {
                        voiceMapping.Remove(nullKey);
                    }
                    
                    if (nullKeys.Count > 0)
                    {
                        SLog.Message(string.Format("[SocialInteractions] Cleaned up {0} voice assignments for missing pawns.", nullKeys.Count));
                    }
                }
            }
        }

        public string GetOrAssignVoice(Pawn pawn)
        {
            if (pawn == null) return "alloy";

            string assignedVoice;
            if (voiceMapping.TryGetValue(pawn, out assignedVoice))
            {
                // Only return if it's not empty
                if (!string.IsNullOrEmpty(assignedVoice)) return assignedVoice;
            }

            // If we don't have available voices loaded yet, we can't assign a PERMANENT one correctly
            // because we don't know what's available.
            // But we can fallback to "alloy" (generic) without saving it, 
            // OR we can try to trigger a load?
            
            if (availableVoices == null || availableVoices.Count == 0)
            {
                // Fallback to default, do NOT save it yet
                return pawn.gender == Gender.Female ? "alloy" : "alloy"; // Placeholder defaults
            }

            // Allocate a new voice
            string newVoice = AssignNewVoice(pawn);
            if (!string.IsNullOrEmpty(newVoice))
            {
                 voiceMapping[pawn] = newVoice;
                 return newVoice;
            }

            return "alloy";
        }
        
        private string AssignNewVoice(Pawn pawn)
        {
            // Filter available voices based on gender
            List<string> candidates = new List<string>();
            // Include both American and British voice variants
            string[] prefixes = pawn.gender == Gender.Female 
                ? new string[] { "af_", "bf_" } 
                : new string[] { "am_", "bm_" };

            foreach (var voice in availableVoices)
            {
                // Check if voice matches any of the gender-appropriate prefixes
                // Handle both with and without file extensions
                string voiceForPrefixCheck = voice;

                // If the voice ends with a file extension, extract the base name for prefix checking
                if (voice.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                    voice.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
                {
                    int extIndex = voice.LastIndexOf('.');
                    if (extIndex > 0)
                    {
                        voiceForPrefixCheck = voice.Substring(0, extIndex); // Remove extension for prefix checking
                    }
                }

                foreach (string prefix in prefixes)
                {
                    if (voiceForPrefixCheck.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(voice); // Add the original voice name (with extension) to candidates
                        break; // No need to check other prefixes for this voice
                    }
                }
            }
            
            // If no gender-specific matches, maybe use any voice?
            if (candidates.Count == 0 && availableVoices.Count > 0)
            {
                // If strictly "af"/"am", we might find nothing. 
                // But let's assume if we have voices, we might want to use them.
                // For now, let's strict filter, but fallback to ANY if none match prefix? 
                // User said: "make sure... voices starting with af_ are female..."
                // Implies we SHOULD respect it.
                
                // If no matching gender voices found, return default (don't assign random mismatch)
                return "alloy"; 
            }

            if (candidates.Count > 0)
            {
                return candidates.RandomElement();
            }

            return "alloy";
        }

        public static void SetAvailableVoices(List<string> voices)
        {
            availableVoices = voices;
        }
    }
}
