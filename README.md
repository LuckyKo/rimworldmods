# Social Interactions Mod for RimWorld

[![RimWorld Version](https://img.shields.io/badge/RimWorld-1.5%20%7C%201.6-blue)](https://rimworldgame.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

Enhance your RimWorld experience with dynamic, AI-generated social interactions, a comprehensive dating system, and immersive combat taunts.

## Features

### AI-Powered Social Interactions
- **LLM Integration**: Uses large language models to generate realistic dialogue between pawns
- **Multiple API Support**: Compatible with KoboldCpp, Ollama, LM Studio, OpenAI and others
- **Rich Context**: Dialogues consider pawn traits, mood, relationships, health, and world conditions
- **Conversation History**: Pawns remember previous conversations for more meaningful interactions
- **Customizable Prompts**: Fine-tune dialogue generation with editable prompt templates

### Advanced Dating System
- **Realistic Dating Mechanics**: Pawns can ask others on dates with acceptance based on opinion and mood
- **Date Activities**: Partners follow and participate in joy activities together
- **Intimate Interactions**: Dates can progress to romantic encounters with animations
- **Relationship Dynamics**: Complex compatibility calculations based on traits, age, and libido
- **Cheating Consequences**: Catch partners cheating and face dramatic consequences

### Combat Taunts
- **Action Commentary**: Pawns shout taunts during melee and ranged combat
- **Battle Reactions**: Vocalize pain when taking damage and call for help when downed
- **Personality-Driven**: Different pawn types have unique combat expressions
- **Configurable Frequency**: Adjust how often taunts occur

### Advanced Drama Systems
- **Gossip/Badmouthing System**: Pawns share negative opinions about others, potentially forming gossip partnerships and strengthening bonds
- **Enhanced Insults**: Severity-based insults that escalate based on opinion, with potential for social fight escalation
- **Strategic Backstabbing**: Manipulative pawns can turn allies against each other through deception and social skill
- **Admiration System**: Low-influence pawns praise leaders based on shared traits/skills to build relationships
- **Make Up/Apologizing System**: Colonists can initiate reconciliation conversations to resolve conflicts and repair damaged relationships

### UI Enhancements
- **Custom Speech Bubbles**: Visually distinct bubbles for different interaction types
- **Chat Log**: Review all interactions in a dedicated chat log window
- **Color Coding**: Different colors for different interaction types (orange for high priority)
- **Text Formatting**: Rich text formatting with colored names and emphasis markers
- **Custom Pawn Bios**: Add personalized text descriptions to pawns accessible via the character card

## Supported APIs

| API | Description |
|-----|-------------|
| **KoboldCpp** | Local LLM inference server |
| **Ollama** | Easy-to-use local LLM platform |
| **LM Studio** | Local LLM experimentation platform |
| **OpenAI** | Cloud-based GPT models |
| **Gemini** | Cloud-based GPT models |
| **Qwen** | Cloud-based GPT models |
| **Deepseek** | Cloud-based GPT models |
| **Grok** | Cloud-based GPT models |
| **Claude** | Cloud-based GPT models |

## Installation

1. Download the mod from [GitHub Releases](https://github.com/LuckyKo/rimworldmods/releases)
2. Extract to your RimWorld Mods folder
3. Enable the mod in the mod list
4. Launch the game and configure settings in `Options > Mod Settings > Social Interactions`

## Configuration

### LLM Settings
- Enable/disable LLM interactions
- Choose API type (KoboldCpp, Ollama, LM Studio, OpenAI)
- Configure API URL and authentication keys
- Adjust generation parameters (temperature, max tokens, etc.)
- Customize prompt templates for dialogues and monologues

### Feature Toggles
- Enable/disable combat taunts
- Enable/disable dating system
- Enable/disable drama interactions (badmouthing, enhanced insults, backstabbing, admiration)
- Control which interaction types use LLM
- Adjust dating probabilities and cooldowns
- Configure visual settings for speech bubbles
- Customize prompt templates to include custom pawn bios

### Performance Options
- Prevent spam mode to avoid overlapping dialogues
- Enable early LLM requests for better performance
- Adjust text rendering style (drop shadow vs background)

## How to Use

### Enhanced Social Interactions
Pawns will automatically engage in LLM-generated conversations during:
- Chitchat and Deep Talk
- Insults and Romance Attempts
- Marriage Proposals and Reassurance
- Disturbing Conversations
- Medical interactions (Tend Patient, Visit Sick Pawn)

### Additional Drama Interactions
Pawns can engage in new interactions that promote the formation of cliques and group leaders:

#### Gossip/Badmouthing System
- Pawns with negative opinions about others will share these views with others during social interactions
- If both pawns share negative opinions about a third party, they form gossip partnerships that strengthen their bond
- If the recipient values the target more than the initiator, the recipient may lose respect for the initiator instead
- If the recipient values the target less than the initiator, the recipient will believe the badmouthing and think worse of the target

#### Enhanced Insults
- Insults are severity-based (Mild, Moderate, Severe, Violent) depending on the initiator's opinion of the recipient
- Higher severity insults have greater chances to escalate into social fights
- Outcome depends on recipient's mood, traits, and relationship with the initiator

#### Strategic Backstabbing
- Successfully badmouthing someone may trigger strategic backstabbing attempts against that person's allies
- Manipulative pawns approach allies of the target to turn them against the original target
- Success depends on the difference between social skills of instigator and target
- Catastrophic betrayal can occur: the more trusted the target was, the more devastating the betrayal (opinions can go from +80 to -100)

#### Admiration System
- Pawns with low social influence will praise and admire those they see as leaders
- Based on shared traits, skills, and roles within the colony
- Success depends on the initiator's social skill level
- Can lead to positive opinion changes when executed well

### Dating System
1. Pawns will naturally attempt to go on dates with others
2. Dates progress through Joy and Lovin stages automatically
3. Compatible couples may engage in intimate interactions

### Manual Interactions
Click on the "Have Chat With" button in a pawn's action bar and select another pawn to manually initiate conversations between two pawns.

### Combat Taunts
Pawns will automatically shout during combat situations:
- When attacking enemies
- When taking damage
- When going down in battle

### Custom Pawn Bios
Add personalized flavor text to individual pawns to enhance AI interactions:
- Click the "Bio" button on any colonist's character card to open the bio editor
- Add custom descriptive text about the pawn (appearance, personality, backstory, etc.)
- Text is saved and persists between game sessions
- Edit or clear existing bio text at any time
- Use the `[pawn1_bio]`, `[pawn2_bio]`, etc. placeholders in prompt templates to include this custom text in LLM interactions
- Alternatively, the "Reset Templates" button in settings will reset the default prompt templates that include the new character bio fields

### Chat Log
Access all generated conversations through the dedicated chat log window:
- Click "Open Chat Log Window" in mod settings
- Or use the "Chat Log" button in the main tabs

## Technical Details

### Architecture
The mod uses Harmony patches to integrate with RimWorld's systems:
- Interaction patches for social dialogue
- Drama interaction patches for badmouthing, enhanced insults, backstabbing, and admiration
- Job driver patches for dating mechanics and strategic backstabbing attempts
- Combat patches for taunts and reactions
- Map components for lifecycle management

### Performance Considerations
- Asynchronous LLM requests to prevent game freezing
- Conversation queuing system to manage dialogue flow
- Configurable spam prevention to avoid overwhelming text
- Efficient tick handling for dating system monitoring

### Compatibility
- Works with RimWorld 1.5 and 1.6
- Compatible with most other mods
- Special handling for Anomaly DLC features
- Designed to be non-invasive to vanilla gameplay

## Development

### Building from Source
1. Clone the repository
2. Ensure you have the RimWorld development environment set up
3. Reference the required RimWorld assemblies
4. Build using your preferred C# compiler

### Contributing
1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

### Architecture Overview
See [architecture.md](architecture.md) for detailed information about the mod's design and implementation.

## Troubleshooting

### Common Issues
- **No dialogue appears**: Check that LLM interactions are enabled and API settings are correct
- **Connection errors**: Verify API URL and authentication keys
- **Performance issues**: Enable spam prevention and adjust LLM settings
- **Missing features**: Ensure all required mods are enabled (Biotech for pregnancy, etc.)

### Logging
Enable verbose logging in mod settings to get detailed diagnostic information.

## Credits

- **Author**: LuckyKo
- **Contributors**: QWEN CLI
- **Inspiration**: Various RimWorld social enhancement mods

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

For issues, feature requests, or questions:
1. Check the [Issues](https://github.com/LuckyKo/rimworldmods/issues) section
2. Submit a new issue with detailed information
3. Include your Player.log file if reporting bugs

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and updates.

---

**Note**: This mod requires an LLM API to function. For local use, we recommend LM Studio or KoboldCpp for the best experience. Make sure to use a model that can fit your GPU VRAM together with the game. My preferred model to use is L3-8B-Stheno-v3.2 but any of the gemma-3-4B or qwen3-4B work just as well. A context window size of 2k is usually enough for this mod.

---
