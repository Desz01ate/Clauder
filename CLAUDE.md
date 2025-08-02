# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Clauder is a .NET 9.0 console application that provides a terminal UI for browsing and managing Claude projects and sessions. It's distributed as a global .NET tool with the command name `clauder`.

## Build and Development Commands

```bash
# Build the project
dotnet build

# Run the application locally
dotnet run --project Clauder

# Run tests
dotnet test

# Clean build artifacts
dotnet clean

# Pack as NuGet tool
dotnet pack

# Install as global tool (for testing)
dotnet tool install --global --add-source ./nupkg Clauder
```

## Architecture Overview

### Core Application Structure

The application follows a layered architecture with dependency injection:

1. **Application Host** (`ApplicationHost.cs`): Main orchestrator that manages the render loop, navigation, and toast notifications using concurrent operations via the Concur library
2. **Page-Based Navigation**: Stack-based navigation system with `PageManager` handling page lifecycle
3. **Render Engine**: Console-based rendering with `ConsoleRenderEngine` and `LayoutManager`
4. **Data Layer**: `ClaudeDataService` handles reading Claude project/session data from `~/.claude/` directory

### Key Components

- **Pages**: `ProjectsPage`, `SessionsPage`, `SettingsPage`, `ClaudeCodePage` - each implements `IPage` interface
- **Services**: All services are registered in `ServiceConfiguration.cs` and use interfaces for testability
- **Models**: `ClaudeConfiguration`, `ClaudeProjectSummary`, `ClaudeProjectWithSessions`, `ClaudeSessionMetadata`
- **Navigation**: Channel-based command system for navigation and toast notifications

### Data Flow

1. Application reads Claude projects from configured directory (default: `~/.claude/`)
2. Projects are parsed and cached with file system watching for updates
3. UI navigates through: Projects → Sessions → Launch Claude Code
4. Configuration is stored in user's config directory

### Key Libraries

- **Spectre.Console**: Terminal UI rendering (v0.50.1-preview)
- **Concur**: Async/await concurrency patterns (v1.4.0-beta)
- **System.Reactive**: Reactive extensions for file watching (v6.0.1)
- **Microsoft.Extensions.DependencyInjection**: Service container (v9.0.0)

### Configuration

- Default config created on first run at `ConfigurationService.GetConfigFilePath()`
- Configurable Claude executable path and data directory
- Settings accessible via 'O' key from main screen

### Testing

- Test project: `Clauder.Tests`
- Unit tests for models, services, and extensions
- Test structure mirrors main project organization