# Social Interactions Mod Architecture

## Overview

The SocialInteractions mod enhances RimWorld's social dynamics by integrating LLM-generated dialogue, adding a complex dating and cheating system, and implementing combat taunts. It uses Harmony patches to intercept and modify vanilla game behavior.

## Development Notes
When debugging RimWorld Harmony patches, if a patch isn't applying, follow these steps:
1.  Verify the target method signature in the `[HarmonyPatch]` attribute is perfect. Use a decompiler to get the exact signature from the game's assembly.
2.  The decompiled file very large, use a helper script (like `extract_class.py`) to extract just the class definition needed, or use read_chunk.py to read a portion of it
3.  If patching a method directly fails, try patching a higher-level method that calls it.
4.  For private methods, use `AccessTools.Method` to get the `MethodInfo` for the patch attribute.
5.  To access private fields within a patch, use `Traverse.Create(__instance).Field("fieldName").GetValue<FieldType>()`.
6.  Ensure the C# language version used in the mod code is compatible with the compiler version being used (the compiler only supports C# 5).
7.  Crucially, always add any new `.cs` source files to the `compile.rsp` response file so they are included in the compilation.
8.  Going forward, for all logging in the SocialInteractions mod, use the custom SLog class (SLog.Message, SLog.Warning, SLog.Error) instead of Verse.Log. This is to ensure consistency and allow for verbose logging control via mod settings.
9.  Freely generate and use helper python scrips if the basic CLI tools fail

### Line Replacement Utility

For precise code modifications, a Python tool for code editing that bypasses exact matching using line numbers and patterns has been provided smart_edit.py

Usage:
    smart_edit.py <file> insert_at_line <line_number> <content_file>
    smart_edit.py <file> replace_block <start_line> <end_line> <content_file>
    smart_edit.py <file> insert_after_pattern <pattern> <content_file>
    smart_edit.py <file> insert_before_pattern <pattern> <content_file>
    smart_edit.py <file> delete_lines <start_line> <end_line>
    smart_edit.py <file> wrap_block <start_line> <end_line> <prefix> <suffix>
    smart_edit.py <file> undo

This utility is especially helpful for making targeted modifications to files when the basic edit tool fails repeatedly


## Core Components

### 1. `SocialInteractions.cs` (Core Logic)
- **Static class** managing mod-wide state and core functionality.
- **Harmony Patches**: Applies all Harmony patches on startup.
- **LLM Interaction Logic**:
  - `IsLlmInteractionEnabled`, `IsLlmJobEnabled`: Determine if an interaction/job should use the LLM based on extensive settings.
  - `GenerateDeepTalkPrompt`, `GenerateMonologuePrompt`: Constructs detailed prompts for the LLM using pawn (traits, mood, genes, skills, etc.) and world data (date, time, weather). Now includes recent conversation history via the `[pawn1_journal]` and `[pawn2_journal]` placeholders.
  - `HandleInteraction`, `HandleNonStoppingInteraction`, `HandleJobGiverInteraction`, `HandleMonologue`: Entry points for triggering LLM interactions, managing asynchronous calls, parsing responses, and queuing speech bubbles.
  - `HandleCaughtCheatingInteraction`: A special handler that holds the cheating pawn in place, triggers a specific LLM interaction, and schedules a delayed fight between the pawns.
  - `HandleThreewayLovinInteraction`: Handles special 3p action scenarios with LLM dialogue.
  - Text utility methods (`WrapText`, `EstimateReadingTime`, `RemoveRichTextTags`, `FormatLlmText`).
- **Pawn Data Helpers**: Private methods (`GetRelationship`, `GetDislikes`, `GetAfflictions`, etc.) to extract relevant pawn information for prompts. Includes `GetLastSocialLogEntry` to extract recent conversation history between pawns. Also includes `GetPawnFlavorText` and `SetPawnFlavorText` for custom bio text management. The custom bio text is integrated into the prompt system through the `[pawn#_bio]` placeholder in the `ExtractPawnData` method.
- **Custom Pawn Bio System**: Static dictionary `PawnFlavorTexts` for storing bio text, with `GetPawnFlavorText` and `SetPawnFlavorText` methods for retrieval and storage using pawn IDs as keys.
- **Multi-API Support**: Added support for multiple LLM API types (KoboldCpp, Ollama, LMStudio, OpenAI) with `GenerateTextWithApiClient` method.

### 2. `SpeechBubbleManager.cs` (UI/Display)
- **GameComponent** managing the display and queuing of speech bubbles.
- **Queuing System**: Ensures sequential display of multi-line LLM dialogue.
- **Spam/Busy Management**: Prevents new LLM interactions from firing while one is already in progress, falling back to default bubbles.
- **Threading**: Uses locks to safely manage shared queues (`speechBubbleQueue`, `pendingJobs`) across asynchronous LLM calls and the main game thread.
- **Display Methods**: `Enqueue` (for sequential), `EnqueueInstant` (for immediate, e.g., taunts), `ShowDefaultBubble` (for non-LLM summaries).
- **Conversation Management**: Tracks conversation IDs and active conversations to prevent overlapping dialogues.
- **Chat Log Integration**: Integrates with `ChatLogManager` to store all interactions for later review.
- **Efficiency System**: Implements scheduled unlock timing to optimize LLM request handling with `ScheduleUnlock` method.

### 3. `DatingManager.cs` (Dating State Machine)
- **Static class** managing the high-level state of ongoing dates.
- **Date Tracking**: Maintains a list of active `Date` objects (initiator, partner, stage: `Joy`, `Lovin`, `Finished`).
- **Lifecycle Management**: `StartDate`, `EndDate`, `RejectDate`, `AdvanceDateStage`. These methods are the "mutations" to the date state.
- **State Checks**: `IsOnDate`, `IsOnDateCooldown`, `GetPartnerOfDateWith`, `GetInitiatorOfDateWith`.
- **Core Date Logic**:
  - `TransitionToLovin`: Handles the transition from the "Joy" stage to the "Lovin" stage, finding a bed and starting `JobDriver_DateLovin`.
  - `CalculateDateCompatibility`, `CalculateSexualCompatibility`: Determines if pawns are compatible for a date/lovin'.
  - `FindSuitableBedForLovin`: Finds an appropriate location for the lovin' activity, either a bed or a random spot nearby.
- **Persistence**: `ExposeData` for saving/loading date state.
- **Maintenance**: `CleanupExpiredDateCooldowns`, `CheckForStuckDates`.
- **Stage Management**: Handles date stage transitions with proper timing and job management.
- **3p Actions**: Support for threeway actions with special handling for spouse involvement.

### 4. `DateTracker_MapComponent.cs` (Date Lifecycle Engine)
- **MapComponent** that acts as the primary engine for progressing dates. It runs continuously, monitoring pawns and calling state changes on the `DatingManager`.
- **Core Functionality**: 
  - **Lifecycle Monitoring**: Continuously checks the status of all pawns on dates, ensuring dates progress correctly through their stages.
  - **Stage Advancement Logic**: Determines when to call `DatingManager.AdvanceDateStage` based on conditions like the initiator's joy need being satisfied or a pawn being drafted/downed.
  - **Partner Activity Management**: Manages the date partner's behavior during the "Joy" stage, primarily by assigning and managing the `FollowAndWatch` job.
  - **Joy Activity Coordination**: Attempts to have the partner join the initiator's joy activity when appropriate.

### 5. `Dating_MapComponent.cs` (Hediff Cleanup)
- **MapComponent** with a specific purpose: cleaning up orphaned `SI_Naked` hediffs and managing 3p action scenarios.
- **Functionality**: Ticks every frame, checking for pawns that have the `SI_Naked` hediff but are no longer doing the `JobDriver_DateLovin` job or `JobDriver_CaughtCheating` job.
- **Grace Period**: Provides a grace period for pawns to transition into the correct job before removing the hediff.
- **3p Action Support**: Handles special cases for 3p actions where multiple pawns may have the SI_Naked hediff.

### 6. `KoboldApiClient.cs`, `OllamaApiClient.cs`, `LMStudioApiClient.cs`, `OpenAiApiClient.cs` (LLM Communication)
- **Classes** handling communication with various external LLM APIs.
- **Data Contracts**: Defines API request/response structures for serialization.
- **`GenerateText`**: Main method to send a prompt to the API and receive a response.
- **Error Handling**: Robust error handling for network issues and API failures.
- **Sampling Parameters**: Support for advanced sampling parameters like Top-K, Top-P, Min-P.

### 7. `SLog.cs` (Logging)
- **Static class** providing a wrapper around `Verse.Log` with a verbosity toggle based on mod settings.
- **Conditional Logging**: Only outputs messages when verbose logging is enabled in the mod settings.

### 8. `SocialInteractionsSettings.cs` (Configuration)
- **`SocialInteractionsModSettings`**: Holds all configurable options (API keys, flags for features/interactions, prompt template, UI/UX settings).
- **`SocialInteractionsMod`**: Implements the in-game settings UI.
- **Extensive Configuration**: Numerous settings for fine-tuning all aspects of the mod's behavior, from dating mechanics to LLM parameters.
- **Multi-API Support**: Configuration options for different LLM API types with their specific settings.

### 9. `ChatLogManager.cs` (Chat History)
- **Static class** managing the storage and retrieval of all chat messages.
- **ChatMessage Class**: Represents individual messages with speaker, recipient, timestamp, type, and formatting information.
- **Message Types**: Supports different message types (LLMChat, GameEvent, DateEvent, CombatEvent) for filtering.
- **Integration**: Works with SpeechBubbleManager to store all interactions for later review in the chat log window.

### 10. `PlayLogEntry_Badmouthing.cs` (Custom Play Log Entry)
- **Custom PlayLogEntry** for badmouthing interactions that includes information about the target pawn.
- **Extended Functionality**: Overrides `ToGameStringFromPOV` to include target pawn information depending on the perspective of the pawn viewing the log.
- **Perspective Handling**: Formats the log text differently based on whether the viewer is the initiator, recipient, target, or third party.
- **Serialization Support**: Includes parameterless constructor and proper serialization methods for RimWorld's save/load system.

### 11. `1.5/Defs/InteractionDefs_Badmouthing.xml` (Interaction Definitions)
- **XML Definition File** containing the definitions for both badmouthing and enhanced insult interactions.
- **Badmouthing Definition**: Defines the Badmouthing interaction type with custom worker class and log rules.
- **EnhancedInsult Definition**: Defines the EnhancedInsult interaction type with severity-based worker class and log rules.
- **Visual Elements**: Specifies appropriate symbols and labels for the interactions in the game UI.
- **Log Rules**: Provides basic log entry templates that are enhanced by custom PlayLogEntry classes.

### 12. `DramaInteractionPatches.cs` (Drama System Patch Controller)
- **Harmony Patch System**: Patches `Pawn_InteractionsTracker.TryInteractWith` to intercept social interactions and potentially replace them with drama interactions.
- **Priority Management**: Implements a priority-based system where badmouthing/gossip has higher priority than enhanced chitchat insults.
- **Conditional Triggers**: Only triggers on suitable interactions (Chitchat, DisturbingChat, Insult) when drama features are enabled.
- **Badmouthing Trigger Logic**: Checks for trait-based encouragement/prevention, mood, and opinion dynamics to determine if badmouthing should occur.
- **Enhanced Chitchat Insult Trigger Logic**: Evaluates mood, opinion of recipient, traits, and opinion differences to determine if enhanced insults should occur.
- **Trait Integration**: Considers traits that prevent negative interactions (Kind, etc.) or encourage them (Jealous, Abrasive, etc.).
- **Prevention Mechanisms**: Ensures that drama interactions only occur when appropriate based on pawn relationships and settings.

### 13. `InteractionWorker_Badmouthing.cs` (Badmouthing System)
- **Custom Interaction Worker**: Handles the core logic of badmouthing interactions that selects a target pawn and determines outcomes based on opinion dynamics.
- **Smart Target Selection**: Uses `GetLeastFavoritePawn` to identify the most disliked pawn in the colony for badmouthing, preventing the recipient from being the target.
- **Gossip Scenario**: When both initiator and recipient share negative opinions about the target, they bond over their shared dislike.
- **Opinion-Based Outcomes**:
  - If recipient values the target pawn less than the initiator, the recipient believes the badmouthing and forms a worse opinion of the target.
  - If recipient values the target pawn more than the initiator, the recipient loses trust in the initiator.
- **Gossip Partnership Formation**: Creates stronger bonds between pawns who share negative opinions about others.
- **Trait Integration**: Considers traits that encourage or prevent badmouthing (e.g., Kind trait prevents it, Jealous/Abrasive traits encourage it).
- **Backstabbing Trigger**: Successful badmouthing may trigger strategic backstabbing attempts against the target's allies.
- **Custom Play Log Entry**: Uses `PlayLogEntry_Badmouthing` for proper logging with target pawn information.
- **LLM Integration**: Generates appropriate subject text for LLM dialogue based on the specific badmouthing scenario and opinion dynamics.
- **Drama Event Tracking**: Adds badmouthing events to the chat log for review via `ChatLogManager.AddDramaEvent`.

### 14. `InteractionWorker_EnhancedInsult.cs` (Enhanced Insult System)
- **Severity-Based Interaction**: Implements insult severity levels (Mild, Moderate, Severe, Violent) based on initiator's opinion of recipient.
- **Social Fight Escalation**: Can escalate insults to physical social fights based on severity, mood, and recipient traits.
- **Thought Application**: Applies different thoughts based on insult severity (e.g., WasToldNegativeThings, HeardBadmouthing).
- **Trait Recognition**: Identifies pawns that enjoy negative interactions or are likely to fight back.
- **LLM Integration**: Generates detailed subject text for LLM dialogue based on severity and whether fights occurred.
- **Custom Play Log Entry**: Uses `PlayLogEntry_EnhancedInsult` for proper logging with severity information.

### 15. `PlayLogEntry_EnhancedInsult.cs` (Custom Play Log Entry for Enhanced Insults)
- **Custom PlayLogEntry** for EnhancedInsult interactions that includes severity and fight escalation information.
- **Severity-Based Descriptions**: Provides different action descriptions based on the severity level of the insult.
- **Fight Outcome Tracking**: Records whether the insult led to a physical confrontation.
- **Perspective Handling**: Formats the log text differently based on the viewer's relationship to the interaction (initiator, recipient, or third party).
- **Serialization Support**: Includes parameterless constructor and proper serialization methods for RimWorld's save/load system.

### 16. `InteractionWorker_Admiration.cs` (Admiration System)
- **Custom Interaction Worker**: Handles admiration interactions where low social influence pawns praise those they see as leaders.
- **Social Hierarchy Recognition**: Identifies pawns with leadership traits, skills, or roles that make them admirable to others.
- **Trait/Skill Matching**: Evaluates shared traits, valued skills, and compatibility between initiators and recipients.
- **Social Skill-Based Success**: Success of admiration attempts depends on the initiator's social skill level.
- **Opinion Change Mechanics**: Can result in positive opinion changes when executed successfully, or neutral/negative outcomes if poorly executed.
- **Admiration Types**: Supports different types of admiration (GeneralPraise, SharedInterestPraise, SkillBasedAdmiration, InspirationalPraise) based on relationship dynamics.
- **Thought Application**: Applies appropriate thoughts to both initiator and recipient based on interaction outcomes.
- **LLM Integration**: Generates subject text for LLM dialogue based on admiration type and outcome.
- **Custom Play Log Entry**: Uses `PlayLogEntry_Admiration` for proper logging of admiration interactions.

### 17. `PawnFlavorText_GameComponent.cs` (Pawn Bio Storage)
- **GameComponent** for saving and loading custom pawn bio text with the entire game state.
- **Persistence**: Uses `Scribe_Collections.Look` to save the `pawnFlavorTexts` dictionary across game sessions, ensuring data persists between different maps and game restarts.
- **Sync Method**: `SyncWithStaticDictionary` method to synchronize data between the component and static dictionary in `SocialInteractions`.

### 18. `PlayLogEntry_Admiration.cs` (Custom Play Log Entry for Admiration)
- **Custom PlayLogEntry** for Admiration interactions that includes admiration type information.
- **Admiration Type Tracking**: Records the type of admiration (GeneralPraise, SharedInterestPraise, SkillBasedAdmiration, InspirationalPraise) in the log entry.
- **Perspective Handling**: Formats the log text differently based on the viewer's relationship to the interaction (initiator, recipient, or third party).
- **Serialization Support**: Includes parameterless constructor and proper serialization methods for RimWorld's save/load system.
- **Extended Functionality**: Overrides `ToGameStringFromPOV` to include admiration type information depending on the perspective of the pawn viewing the log.

### 19. `InteractionWorker_Backstabbing.cs` (Strategic Backstabbing System)
- **Custom Interaction Worker**: Handles the core logic of strategic betrayal where manipulative pawns turn allies against each other.
- **Social Skill-Based Success**: Success rate depends on the difference between the instigator's and target's social skills.
- **Information Gathering Phase**: Can perform an initial information gathering phase to learn about relationship dynamics before attempting manipulation.
- **Catastrophic Betrayal Mechanics**: Successful backstabbing causes massive opinion reversal based on original trust level - the more trusted the target was, the more devastating the betrayal.
- **Target Selection Logic**: Identifies the target's "best friends" or highest-opinion allies for the strategic backstabbing attempt.
- **Trait Integration**: Pawns with manipulation-related traits (manipulative, deceptive, calculating, psychopath) are more likely to attempt or succeed at backstabbing.
- **Job System Integration**: Uses job.targetB and ScheduledTargetPawn to preserve target information through the job system to prevent targeting errors.
- **LLM Integration**: Generates appropriate subject text for LLM dialogue based on the strategic nature of the betrayal and its success/failure.
- **Custom Play Log Entry**: Uses `PlayLogEntry_Backstabbing` for proper logging with target and success information.
- **Data Management**: Efficiently stores bio text using pawn `thingIDNumber` as the key, working across all maps in the game.
- **Initialization Methods**: `FinalizeInit` and `LoadedGame` methods to ensure proper data synchronization when the game starts or loads.

### 17. `Dialog_EditPawnFlavorText.cs` (Bio Editor UI)
- **Window** providing a user interface for editing custom pawn bio text.
- **Text Input**: Clean multi-line text input field for entering bio information.
- **Action Buttons**: "Save", "Cancel", and "Clear" buttons for managing bio text changes.
- **Character-Specific**: Associates bio text directly with the pawn being edited.
- **User Experience**: Simple and intuitive interface accessible from the character card.

### 18. `CharacterCardUtility_AddFlavorTextButton_Patch.cs` (Character Card Integration)
- **Harmony Patch** that adds a "Bio" button to the character card for accessing the bio editor.
- **Positioning**: Places the button one row down from the standard character card buttons.
- **UI Integration**: Seamlessly integrates with the existing character card UI.
- **Action Handling**: Triggers the `Dialog_EditPawnFlavorText` when clicked.

### 19. `Game_FlavorTextComponent_Patch.cs` (Game Initialization)
- **Harmony Patches** for `Game.InitNewGame` and `Game.LoadGame` to properly initialize the `PawnFlavorText_GameComponent`.
- **New Game Initialization**: Ensures the GameComponent is created when starting a new game.
- **Game Loading Support**: Guarantees the GameComponent exists when loading an existing game.
- **Persistence Coordination**: Works with `PawnFlavorText_GameComponent` to maintain proper data flow.

### 20. Localization System
- **Translation Framework**: Implements RimWorld's standard keyed translation system with `Languages/English/Keyed/Keyed.xml` structure.
- **Multi-Language Support**: Includes full Chinese Simplified localization in `Languages/ChineseSimplified/Keyed/Keyed.xml`.
- **Comprehensive Coverage**: Translates all mod settings, UI elements, dialog text, and descriptions using translation keys.
- **Settings Integration**: Updates `SocialInteractionsSettings.cs` to use `.Translate()` method calls for all user-facing text.
- **File Structure**: Proper RimWorld localization structure with separate language folders and keyed XML files.
- **UI Elements Translated**: Includes settings labels, descriptions, bio editor dialog elements, and all other user-facing strings.

## Job Drivers

### `JobDriver_GoOnDate.cs`
- **Custom JobDriver** that initiates the dating sequence.
- **Acceptance Logic**: Rolls for date acceptance based on opinion and mood.
- **Job Assignment**: Finds a joy job for the initiator and assigns a `FollowAndWatch` job to the partner.
- **Validation**: Comprehensive validation of pawn states throughout the job.

### `JobDriver_DateLovin.cs`
- **Custom JobDriver** for the "Lovin" stage of a date.
- **Animation**: Provides bouncing animation for both pawns during the lovin' activity.
- **Hediff Management**: Applies and removes the `SI_Naked` hediff.
- **Thought Management**: Gives appropriate thoughts to both pawns after the activity.
- **Pregnancy Handling**: Handles pregnancy mechanics for Biotech-enabled games.
- **Stage Completion**: Advances the date to the finished stage upon completion.
- **Efficiency**: Optimized tick handling with proper cleanup.

### `JobDriver_FollowAndWatch.cs`
- **Custom JobDriver** for the date partner to follow the initiator during the joy stage.
- **Pathing Logic**: Continuously updates the path to follow the initiator.
- **Joy Gain**: Provides social joy gain to the partner while following.
- **Stage Transition**: Monitors the initiator's job to determine when to advance the date stage.

### `JobDriver_BackstabbingApproachTarget.cs`
- **Custom JobDriver** for direct strategic backstabbing attempts.
- **Physical Movement**: Pawns physically approach their target to manipulate them against a mutual acquaintance.
- **Pathing Logic**: Continuously updates the path to follow the target during the interaction.
- **State Validation**: Comprehensive validation system ensures targets are in appropriate states for interaction (alive, conscious, not in mental states).
- **Target Preservation**: Maintains the original target information to prevent targeting errors.
- **Interaction Handling**: Executes the backstabbing interaction using `InteractionWorker_Backstabbing`.

### `JobDriver_BackstabbingGatherInfo.cs`
- **Custom JobDriver** for information gathering phase of strategic backstabbing.
- **Intelligence Gathering**: Allows the instigator to learn about the target's relationships before attempting manipulation.
- **Physical Movement**: Pawns physically approach their target to conduct the information gathering conversation.
- **State Validation**: Comprehensive validation system ensures targets are in appropriate states for interaction.
- **Target Preservation**: Maintains target information through the job system.
- **Interaction Handling**: Executes the information gathering using `InteractionWorker_Backstabbing`.

## Harmony Patches (`*.cs` files)

### Interaction & Thought Patches
- **`InteractionWorker_Interacted_Patch.cs`**: Patches `InteractionWorker.Interacted` to call `SocialInteractions.HandleInteraction` for relevant interactions.
- **`InteractionWorkers.cs`**: Defines custom `InteractionWorker` classes (`InteractionWorker_DateLovin`, `InteractionWorker_CaughtCheating`) that trigger specific LLM interactions or game logic.
- **`InteractionWorker_Badmouthing.cs`**: Custom interaction worker for badmouthing interactions that handles target selection, opinion dynamics, and gossip scenarios.
- **`InteractionWorker_EnhancedInsult.cs`**: Custom interaction worker for enhanced insults with severity levels based on opinion, including social fight escalation logic.
- **`ThoughtHandler_OpinionOffsetOfGroup_Patch.cs`**: Patches `ThoughtHandler.OpinionOffsetOfGroup` to apply opinion modifiers from `Thought_CaughtCheating`.
- **`DramaInteractionPatches.cs`**: Patches `Pawn_InteractionsTracker.TryInteractWith` to potentially initiate drama interactions (badmouthing/gossip and enhanced insults) during social interactions based on pawn traits and settings.

### Job & JoyGiver Patches/Implementations
- **`JobDriver_GoOnDate.cs`**: Custom `JobDriver` that initiates the dating sequence (asking, starting joy job, assigning `FollowAndWatch`).
- **`JobDriver_DateLovin.cs`**: Custom `JobDriver` for the "Lovin" stage of a date, applying temporary hediffs and giving thoughts.
- **`JobDriver_FollowAndWatch.cs`**: Custom `JobDriver` for the date partner to follow the initiator during the joy stage.
- **`JobDriver_HaveDeepTalk.cs`, `JobDriver_BeTalkedTo.cs`**: Custom drivers for Deep Talk jobs initiated by pawns.
- **`JoyGiver_GoOnDate.cs`, `JoyGiver_HaveDeepTalk.cs`**: Custom `JoyGiver`s that create the initial jobs for dating and deep talks.
- **`JobPatches.cs`**: Patches `JobDriver.TryMakePreToilReservations` to allow pawns to reserve the same item for social interactions.
- **`JobDriver_Joy_Patch.cs`**: Patches `JobDriver_Joy.MakeNewToils` to allow date partners to potentially join the same joy activity.

### Pawn/Map Lifecycle Patches
- **`Pawn_Tick_Patch.cs`**: Patches `Pawn.Tick` to trigger the scheduled fight after a cheating interaction is complete.
- **`Map_FinalizeInit_Patch.cs`**: Patches `Map.FinalizeInit` to initialize the custom map components (`DateTracker_MapComponent`, `Dating_MapComponent`, `SpeechBubbleManager`).
- **`MindStateTick_Patch.cs`**: Patches `Pawn_MindState.MindStateTick` to handle interrupting pawns for dating.
- **`Pawn_DraftController_Drafted_Patch.cs`**: Patches `Pawn_DraftController.set_Drafted` to interrupt date jobs when a pawn is drafted.

### Combat & Rendering Patches
- **`CombatPatches.cs`**: Patches various combat methods (`CheckMeleeAttackAt`, `TakeDamage`, etc.) to trigger combat taunts and complaints via `SpeechBubbleManager.EnqueueInstant`.
- **`PawnRenderer_GetDrawParms_Patch.cs`, `PawnRenderer_RenderPawnAt_Patch.cs`**: Patches rendering methods to apply visual offsets for pawns engaged in `JobDriver_DateLovin`.

## Data Flow Example: Starting a Date
1.  `JoyGiver_GoOnDate` gives a `JobDriver_GoOnDate` job to an initiator.
2.  `JobDriver_GoOnDate` moves the initiator to a potential partner and rolls for acceptance based on opinion and mood.
3.  If accepted:
    -   `DatingManager.StartDate` is called, creating the `Date` object and applying the `OnDate` hediff.
    -   `SocialInteractions.HandleNonStoppingInteraction` is called for the date acceptance dialogue.
    -   The initiator finds a joy activity (e.g., watching TV).
    -   The partner is given a `JobDriver_FollowAndWatch` job.
4.  `DateTracker_MapComponent` now monitors the date. It ensures the partner keeps following the initiator and attempts to have the partner join joy activities.
5.  When the initiator's joy need is satisfied or they move to a non-joy job, `DateTracker_MapComponent` calls `DatingManager.AdvanceDateStage`.
6.  `DatingManager` transitions the state to `DateStage.Lovin` and calls `TransitionToLovin`.
7.  `TransitionToLovin` finds a suitable location (bed or random spot) and starts `JobDriver_DateLovin` for both pawns.
8.  `JobDriver_DateLovin` runs, applying the `SI_Naked` hediff, showing the animation, and providing joy/thoughts. When it completes, it calls `DatingManager.AdvanceDateStage`.
9.  `DatingManager` transitions the state to `DateStage.Finished` and calls `EndDate`.
10. `DatingManager.EndDate` cleans up hediffs, ends any remaining jobs, and puts the pawns on a date cooldown.

## Data Flow Example: Monologue
1. A pawn experiences a specific event (e.g., becomes a leader, enters a mental state, or a significant world event occurs).
2. The relevant game code calls `SocialInteractions.HandleMonologue` with the pawn and a subject describing the event.
3. `HandleMonologue`:
    - Checks if LLM interactions are enabled and if spam protection is active.
    - Generates a prompt using `GenerateMonologuePrompt`, which includes detailed information about the pawn and the world context.
    - Sends the prompt to the LLM API via the appropriate client.
    - Processes the LLM response, splitting it into individual lines.
    - Queues each line as a speech bubble via `SpeechBubbleManager.Enqueue`.
    - Manages conversation state and timing for a smooth display experience.

## Recent Enhancements

### Multi-API Support
- **Expanded LLM Integration**: Support for multiple LLM API types including KoboldCpp, Ollama, LMStudio, and OpenAI.
- **Flexible Configuration**: Each API type has its own configuration options and model settings.
- **Improved Prompt Generation**: Enhanced prompt templates with comprehensive pawn and world information.

### Enhanced Dating System
- **Three-Way Actions**: Support for 3p actions with special handling for spouse involvement.
- **Improved Compatibility Calculation**: More sophisticated date compatibility calculations based on traits, age, libido, and relationships.
- **Better Location Finding**: Enhanced logic for finding suitable locations for lovin' activities.
- **Robust Error Handling**: Comprehensive null checks and error handling throughout the dating system.

### Improved LLM Integration
- **Conversation Management**: Better tracking of conversation IDs to prevent overlapping dialogues.
- **Enhanced Prompt Generation**: More detailed prompts with comprehensive pawn and world information.
- **Graceful Degradation**: Fallback mechanisms when LLM interactions are disabled or unavailable.
- **Efficiency System**: Scheduled unlock timing to optimize LLM request handling.

### Better Performance and Stability
- **Optimized Tick Handling**: Reduced frequency of expensive operations.
- **Memory Management**: Proper cleanup of resources and references.
- **Extensive Logging**: Detailed logging for debugging (when enabled).
- **Chat Log Integration**: Complete history of all interactions stored for later review.

### Combat Taunts
- **Expanded Taunt System**: Comprehensive combat taunts for melee attacks, ranged attacks, getting hit, and going down.
- **Configurable Probabilities**: Adjustable probabilities for different types of combat taunts.
- **Visual Differentiation**: Combat taunts use different visual styles from regular dialogue.

### Drama Systems
#### Badmouthing System
- **Interaction Definition**: Custom `Badmouthing` interaction defined in `InteractionDefs_Badmouthing.xml` with appropriate worker class and log rules.
- **Custom Interaction Worker**: `InteractionWorker_Badmouthing.cs` handles the core logic of selecting a target pawn and determining outcomes based on opinion dynamics.
- **Smart Target Selection**: Uses `GetLeastFavoritePawn` to identify the most disliked pawn in the colony for badmouthing, preventing the recipient from being the target.
- **Gossip Scenario**: When both initiator and recipient share negative opinions about the target, they bond over shared dislike with positive thoughts.
- **Opinion-Based Outcomes**:
  - If recipient values the target pawn less than the initiator, the recipient is more likely to believe the badmouthing, resulting in reduced opinion of the target.
  - If recipient values the target pawn more than the initiator, the recipient loses trust in the initiator for speaking negatively about someone they respect more.
- **Harmony Patch Integration**: `DramaInteractionPatches.cs` patches `Pawn_InteractionsTracker.TryInteractWith` to potentially trigger badmouthing during suitable social interactions (Chitchat, DisturbingChat, Insult).
- **Trait-Based Triggering**: Considers pawn traits that encourage or prevent badmouthing (e.g., Kind trait prevents it, Jealous/Abrasive traits encourage it).
- **Global Play Log Enhancement**: `PlayLogEntry_Badmouthing.cs` provides detailed target pawn information in the global play log, accessible through the history tab.
- **LLM Integration**: Generates appropriate subject text for LLM dialogue based on the specific badmouthing scenario and opinion dynamics.
- **Drama Event Tracking**: Adds badmouthing events to the chat log for review via `ChatLogManager.AddDramaEvent`.

#### Enhanced Insult System
- **Interaction Definition**: Custom `EnhancedInsult` interaction defined in `InteractionDefs_Badmouthing.xml` with severity-based worker class and log rules.
- **Severity-Based Mechanics**: Determines insult severity (Mild, Moderate, Severe, Violent) based on initiator's opinion of recipient.
- **Social Fight Escalation**: Can escalate to physical social fights based on severity, mood, and recipient traits.
- **Thought Application**: Applies different thoughts based on insult severity.
- **Harmony Patch Integration**: `DramaInteractionPatches.cs` patches `Pawn_InteractionsTracker.TryInteractWith` to potentially trigger enhanced insults during suitable social interactions (Chitchat, DisturbingChat).
- **Trait Recognition**: Identifies pawns that enjoy negative interactions or are likely to fight back.
- **Custom Play Log Entry**: `PlayLogEntry_EnhancedInsult.cs` provides detailed severity and outcome information in the global play log.
- **LLM Integration**: Generates appropriate subject text for LLM dialogue based on severity and fight outcomes.

#### Strategic Backstabbing System
- **Interaction Definition**: Custom `Backstabbing` interaction defined in `InteractionDefs_Badmouthing.xml` with strategic worker class and log rules.
- **Follow-up Mechanism**: Triggered as a strategic follow-up to successful badmouthing/gossip interactions when the instigator identifies valuable targets (high-trust allies of the original target).
- **Social Skill-Based Success**: Success rate depends on the difference between the instigator's and target's social skills, making the system skill-based rather than random.
- **Catastrophic Betrayal**: Successful backstabbing causes massive opinion reversal based on original trust level - the more trusted the target was, the more devastating the betrayal (opinion can go from +80 to -100).
- **Target Selection Logic**: Identifies the original target's "best friends" or highest-opinion allies for the strategic backstabbing attempt. When triggered from badmouthing, preserves the original target information to prevent confusion.
- **Trait Integration**: Pawns with manipulation-related traits (manipulative, deceptive, calculating, psychopath) are more likely to attempt or succeed at backstabbing.
- **Custom Interaction Worker**: `InteractionWorker_Backstabbing.cs` handles the core logic of strategic betrayal with social skill comparisons and opinion reversal mechanics.
- **Custom Play Log Entry**: `PlayLogEntry_Backstabbing.cs` provides detailed information about the betrayal in the global play log.
- **Integration with Existing Systems**: Builds on the existing badmouthing system, checking for backstabbing opportunities after successful drama interactions.
- **LLM Integration**: Generates appropriate subject text for LLM dialogue based on the strategic nature of the betrayal and its success/failure.
- **Settings Integration**: Includes toggle to enable/disable backstabbing and configure base chance for backstabbing attempts.
- **Job System Integration**: Uses custom job drivers (`JobDriver_BackstabbingApproachTarget.cs`, `JobDriver_BackstabbingGatherInfo.cs`) for physical pawn movement and interaction scheduling.
- **Target Preservation**: When backstabbing is scheduled from badmouthing, the original target information is preserved through the job system using `job.targetB` and accessed via `ScheduledTargetPawn` property to prevent target confusion.

#### Backstabbing Settings
- **Enable Backstabbing**: Toggle to enable or disable the entire backstabbing system
- **Base Backstabbing Chance**: Configurable base chance for backstabbing attempts that can be adjusted in the mod settings

#### Backstabbing Job System
- **Custom Job Drivers**: Two specialized job drivers handle the physical aspects of backstabbing:
  - `JobDriver_BackstabbingApproachTarget.cs`: Handles direct backstabbing attempts where the instigator approaches an ally to manipulate them against a mutual acquaintance
  - `JobDriver_BackstabbingGatherInfo.cs`: Handles information gathering phases where the instigator gathers relationship intelligence before attempting manipulation
- **Physical Movement**: Pawns physically move to their targets and maintain interaction even when targets move, using continuous path updating
- **State Validation**: Comprehensive validation system ensures targets are in appropriate states for interaction (alive, conscious, not in mental states) while allowing strategic interruptions of regular activities
- **Target Preservation**: Critical target information is preserved through the job system to prevent the "wrong target" issue where backstabbing would target the wrong pawn

#### Admiration System
- **Custom Interaction Worker**: `InteractionWorker_Admiration.cs` handles admiration mechanics based on social hierarchy and skill matching
- **Custom Play Log Entry**: `PlayLogEntry_Admiration.cs` for proper logging with admiration type information
- **Harmony Patch Integration**: `DramaInteractionPatches.cs` triggers admiration during suitable social interactions
- **Settings Integration**: Toggle and configuration options in `SocialInteractionsSettings.cs` with localization support

#### MakeUp/Apologizing System
- **Custom Interaction Worker**: `InteractionWorker_MakeUp.cs` handles reconciliation mechanics including thought removal and opinion changes
- **Custom Play Log Entry**: `PlayLogEntry_MakeUp.cs` for logging reconciliation attempts with success/failure tracking
- **Harmony Patch Integration**: `DramaInteractionPatches.cs` triggers make-up interactions during social exchanges when negative modifiers exist
- **Settings Integration**: Configuration options in `SocialInteractionsSettings.cs` with localization support

#### Breakup Interaction System
- **Harmony Patch**: `InteractionWorker_Breakup_Patch.cs` intercepts the base game's breakup interaction to add LLM functionality
- **Enhanced Experience**: Adds AI-generated dialogue to the breakup event while preserving all original game mechanics (relationship changes, thoughts, letters, etc.)
- **Settings Integration**: `enableBreakups` setting in `SocialInteractionsSettings.cs` to control the feature
- **Compatibility**: Uses `AccessTools.Method` to safely access internal RimWorld classes without breaking original functionality
- **Fallback Handling**: Maintains base game behavior when LLM features are disabled while still showing appropriate speech bubbles

#### Marriage Ceremony Integration
- **Harmony Patch**: `MarriageCeremonyStart_Patch.cs` intercepts the transition from gathering to actual ceremony phase in `LordJob_Joinable_MarriageCeremony`
- **Timing**: Triggers when the ceremony officially begins (when pawns move to their designated spots to exchange vows), not during the gathering phase
- **Efficiency**: Uses reflection to access transition destination and adds the LLM call as a pre-action, ensuring it runs exactly once
- **Settings Integration**: `enableMarriageCeremony` setting in `SocialInteractionsSettings.cs` with proper localization
- **LLM Integration**: Generates appropriate subject text for LLM dialogue based on the ceremony context