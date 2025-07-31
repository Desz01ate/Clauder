# Clauder

Clauder is a .NET tool that allows you to browse and manage your Claude projects and sessions from your terminal.

## Installation

You can install Clauder as a global .NET tool:

```bash
dotnet tool install --global Clauder
```

## Usage

Once installed, you can run the tool using the following command:

```bash
clauder
```

This will launch an interactive terminal UI that will guide you through the following steps:

1.  **Select a Project:** The tool will display a paginated list of your Claude projects. You can navigate through the list and select the project you want to work with.
2.  **Select a Session:** After selecting a project, the tool will display a list of its sessions. You can then select a session to view.
3.  **Launch Session:** The selected session will be launched.

### Prerequisites

Before running the tool, make sure that you have your Claude projects stored in the expected directory. The tool looks for a `.claude` directory in your user's home directory. For example, on Linux, this would be `~/.claude/`.
