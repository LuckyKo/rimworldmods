# Social Interactions Mod Architecture

## Overview

The SocialInteractions mod enhances RimWorld's social dynamics by integrating LLM-generated dialogue, adding a complex dating and cheating system, and implementing combat taunts. It uses Harmony patches to intercept and modify vanilla game behavior.

## Core Components

### 1. `SocialInteractions.cs` (Core Logic)
- **Static class** managing mod-wide state and core functionality.
- **Harmony Patches**: Applies all Harmony patches on startup.
- **LLM Interaction Logic**:
  - `IsLlmInteractionEnabled`, `IsLlmJobEnabled`: Determine if an interaction/job should use the LLM based on extensive settings.
  - `GenerateDeepTalkPrompt`, `GenerateMonologuePrompt`: Constructs detailed prompts for the LLM using pawn (traits, mood, genes, skills, etc.) and world data (date, time, weather).
  - `HandleInteraction`, `HandleNonStoppingInteraction`, `HandleJobGiverInteraction`, `HandleMonologue`: Entry points for triggering LLM interactions, managing asynchronous calls, parsing responses, and queuing speech bubbles.
  - `HandleCaughtCheatingInteraction`: A special handler that holds the cheating pawn in place, triggers a specific LLM interaction, and schedules a delayed fight between the pawns.
  - Text utility methods (`WrapText`, `EstimateReadingTime`, `RemoveRichTextTags`, `FormatLlmText`).
- **Pawn Data Helpers**: Private methods (`GetRelationship`, `GetDislikes`, `GetAfflictions`, etc.) to extract relevant pawn information for prompts.

### 2. `SpeechBubbleManager.cs` (UI/Display)
- **GameComponent** managing the display and queuing of speech bubbles.
- **Queuing System**: Ensures sequential display of multi-line LLM dialogue.
- **Spam/Busy Management**: Prevents new LLM interactions from firing while one is already in progress, falling back to default bubbles.
- **Threading**: Uses locks to safely manage shared queues (`speechBubbleQueue`, `pendingJobs`) across asynchronous LLM calls and the main game thread.
- **Display Methods**: `Enqueue` (for sequential), `EnqueueInstant` (for immediate, e.g., taunts), `ShowDefaultBubble` (for non-LLM summaries).
- **Conversation Management**: Tracks conversation IDs and active conversations to prevent overlapping dialogues.

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

### 6. `KoboldApiClient.cs` (LLM Communication)
- **Class** handling communication with the external LLM API (KoboldCpp).
- **Data Contracts**: Defines `KoboldApiRequest` and `KoboldApiResponse` for serialization.
- **`GenerateText`**: Main method to send a prompt to the API and receive a response.
- **Error Handling**: Robust error handling for network issues and API failures.

### 7. `SLog.cs` (Logging)
- **Static class** providing a wrapper around `Verse.Log` with a verbosity toggle based on mod settings.
- **Conditional Logging**: Only outputs messages when verbose logging is enabled in the mod settings.

### 8. `SocialInteractionsSettings.cs` (Configuration)
- **`SocialInteractionsModSettings`**: Holds all configurable options (API keys, flags for features/interactions, prompt template, UI/UX settings).
- **`SocialInteractionsMod`**: Implements the in-game settings UI.
- **Extensive Configuration**: Numerous settings for fine-tuning all aspects of the mod's behavior, from dating mechanics to LLM parameters.

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

### `JobDriver_FollowAndWatch.cs`
- **Custom JobDriver** for the date partner to follow the initiator during the joy stage.
- **Pathing Logic**: Continuously updates the path to follow the initiator.
- **Joy Gain**: Provides social joy gain to the partner while following.
- **Stage Transition**: Monitors the initiator's job to determine when to advance the date stage.

## Harmony Patches (`*.cs` files)

### Interaction & Thought Patches
- **`InteractionWorker_Interacted_Patch.cs`**: Patches `InteractionWorker.Interacted` to call `SocialInteractions.HandleInteraction` for relevant interactions.
- **`InteractionWorkers.cs`**: Defines custom `InteractionWorker` classes (`InteractionWorker_DateLovin`, `InteractionWorker_CaughtCheating`) that trigger specific LLM interactions or game logic.
- **`ThoughtHandler_OpinionOffsetOfGroup_Patch.cs`**: Patches `ThoughtHandler.OpinionOffsetOfGroup` to apply opinion modifiers from `Thought_CaughtCheating`.

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
    - Sends the prompt to the LLM API via `KoboldApiClient`.
    - Processes the LLM response, splitting it into individual lines.
    - Queues each line as a speech bubble via `SpeechBubbleManager.Enqueue`.
    - Manages conversation state and timing for a smooth display experience.

## Recent Enhancements

### Enhanced Dating System
- **Three-Way Actions**: Support for 3p actions with special handling for spouse involvement.
- **Improved Compatibility Calculation**: More sophisticated date compatibility calculations based on traits, age, libido, and relationships.
- **Better Location Finding**: Enhanced logic for finding suitable locations for lovin' activities.
- **Robust Error Handling**: Comprehensive null checks and error handling throughout the dating system.

### Improved LLM Integration
- **Conversation Management**: Better tracking of conversation IDs to prevent overlapping dialogues.
- **Enhanced Prompt Generation**: More detailed prompts with comprehensive pawn and world information.
- **Graceful Degradation**: Fallback mechanisms when LLM interactions are disabled or unavailable.

### Better Performance and Stability
- **Optimized Tick Handling**: Reduced frequency of expensive operations.
- **Memory Management**: Proper cleanup of resources and references.
- **Extensive Logging**: Detailed logging for debugging (when enabled).