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
                    voiceMapping = new Dictionary<Pawn, string>();
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
            string prefix = pawn.gender == Gender.Female ? "af_" : "am_";

            foreach (var voice in availableVoices)
            {
                // Basic filtering logic based on user request ("af_" / "am_")
                // If the voice list doesn't follow this convention, we might fall back to anything
                if (voice.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(voice);
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
