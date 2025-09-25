# Changelog

All notable changes to the Social Interactions mod will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2025-09-26

### Changed
- Family field now limits to first-degree relatives (parents/children/siblings/spouse/fiance/lover) and only includes living relatives
- Likes and Dislikes fields now use weighted random selection based on thought intensity instead of first-come basis
- Implemented duplicate prevention in family field to avoid repeated names
- Switched spam protection timing from real-world time to game tick-based scheduling to properly handle game pauses
- Speech bubbles now correctly respect pause state - LLM unlock will wait until game resumes and bubbles finish displaying
- Improved reliability of spam protection system during gameplay interruptions

### Fixed
- Duplicate entries in family field

## [1.0.0] - 2024-09-20

### Added
- Multi-API support for LLM interactions (KoboldCpp, Ollama, LM Studio, OpenAI)
- Comprehensive dating system with Joy and Lovin stages
- Advanced compatibility calculations for dating
- Three-way action support for romantic encounters
- Combat taunt system with melee, ranged, damage, and downed reactions
- Chat log window to review all interactions
- Rich text formatting for speech bubbles
- Conversation history tracking between pawns
- Configurable prompt templates
- Spam prevention and efficiency systems
- Custom speech bubble rendering with animations
- Hediff management for dating states
- Graceful degradation when LLM is unavailable
- Initial release of Social Interactions mod
- Basic LLM integration for social dialogue
- Simple dating mechanics
- Combat taunts
- Core mod infrastructure

[Unreleased]: https://github.com/TODO/TODO/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/TODO/TODO/compare/v1.0.0...v1.0.1