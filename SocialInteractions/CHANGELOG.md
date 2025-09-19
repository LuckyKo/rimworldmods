# Changelog

All notable changes to the Social Interactions mod will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

### Changed
- Enhanced prompt generation with detailed pawn and world context
- Improved error handling and logging throughout the mod
- Optimized tick handling for better performance
- Refined dating stage transitions and job management
- Better null checking and validation in all systems
- Updated settings UI with organized sections
- Improved compatibility with RimWorld 1.5 and 1.6

### Fixed
- Race conditions in speech bubble queuing
- Memory leaks in job management
- Null reference exceptions in edge cases
- Pathing issues in dating activities
- Hediff cleanup problems
- Conversation overlap issues
- Performance problems with frequent ticking

## [1.0.0] - 2024-09-20

### Added
- Initial release of Social Interactions mod
- Basic LLM integration for social dialogue
- Simple dating mechanics
- Combat taunts
- Core mod infrastructure

[Unreleased]: https://github.com/TODO/TODO/compare/v1.0.0...HEAD