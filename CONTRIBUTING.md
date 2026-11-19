# Contributing

Thank you for your interest in Switch Save Assistant.

> Switch Save Assistant is licensed under the [MIT License](LICENSE). The public repository and contribution workflow are being prepared.

## Development setup

Use Windows and the .NET 10 SDK. From the project root:

```powershell
dotnet restore .\SwitchSaveAssistant.slnx
dotnet build .\SwitchSaveAssistant.slnx --no-restore
dotnet test .\SwitchSaveAssistant.Tests\SwitchSaveAssistant.Tests.csproj --no-restore
```

Keep the build warning-free. Add or update focused tests for behavior changes, especially any change affecting file selection, identity resolution, backup or restore behavior.

## Before submitting changes

- Describe expected behavior, actual behavior and reproduction steps.
- Prefer small changes with a clear purpose; document any new configuration or provider assumption.
- Preserve compatibility with the supported existing workflows: Switch ↔ Folder and Eden ↔ Folder.
- Avoid destructive operations in tests unless they run in disposable temporary directories.
- Never attach or commit real saves, private configuration, console/profile databases, local backups or credentials.
- Use fabricated game/profile examples in public test fixtures and screenshots.
- Include the commands run and their results in pull-request descriptions.

## Safety-sensitive changes

Changes to save replacement, alias reconciliation, file integrity checks or rollback must be reviewed particularly carefully. Never silently merge divergent saves or automatically choose a conflicting version based only on file timestamps.

## Scope

Support for emulators other than Eden is currently postponed. Please discuss significant new-provider work before submitting large changes.
