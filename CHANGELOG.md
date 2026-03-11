# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6.0] - 2026-03-11

### Changed
- Redesigned auto-generate bio to produce a structured, multi-section character sheet
  - Dossier section: code-generated factual summary (name, traits, skills, health, family, etc.) shown as a live read-only panel in the dialog
  - Persona section: LLM-generated personality paragraph and quirks/values bullet points
- Bio generation prompt now feeds additional pawn data (mood, afflictions, likes/dislikes, implants) for richer personality output
- Robust response parser with fallback for local models that don't follow formatting instructions
- Enlarged bio dialog window with distinct dossier and persona areas
- Added .csproj build support (dotnet build) using NuGet reference assemblies

### Fixed
- Extra closing brace in InteractionWorker_ConvertIdeoAttempt_Patch.cs

## [1.1.1] - 2025-11-10

### Added
- Version tracking system to settings for better compatibility management

## [1.1.0] - 2025-11-10

### Added
- MakeUp/Apologizing interaction to the drama system
- Trait-based reconciliation mechanics (kind pawns more likely to initiate)
- Integration with core social mechanics (thought removal, opinion changes)
- Present-tense prompt generation for better LLM responses
- Comprehensive settings and configuration options
- Translation support for English and Chinese
- RulePackDef files for reconciliation outcomes
- Meaningful log entries for reconciliation attempts

### Changed
- Enhanced InteractionDef for MakeUp to provide meaningful log entries
- Improved LLM integration that preserves core mechanics when disabled

## [1.0.2] - 2025-10-19

### Added
- Added Claude API support (Anthropic)
- Added Grok API support (xAI)
- Added Deepseek API support
- Added Qwen API support (Alibaba Cloud DashScope)
- Added Gemini API support (Google)
- Added model-specific settings for each new API
- Updated UI to include settings for all new APIs

### Changed
- Updated README with information about new API support
- Enhanced API client infrastructure to support multiple new services
- Improved documentation and code organization

## [1.0.1] - 2025-08-15

### Added
- Initial release with basic LLM integration
- Support for KoboldCpp, Ollama, LMStudio, and OpenAI APIs
- Social interaction features with AI-generated dialogue
- Dating system implementation
- Combat taunts and reactions
- Custom speech bubble system
- Chat log functionality

### Changed
- Initial mod structure and architecture

[1.1.1]: https://github.com/LuckyKo/rimworldmods/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/LuckyKo/rimworldmods/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/LuckyKo/rimworldmods/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/LuckyKo/rimworldmods/releases/tag/v1.0.1