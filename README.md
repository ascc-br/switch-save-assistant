# Switch Save Assistant

A Windows desktop application for inspecting, comparing, backing up and synchronizing Nintendo Switch save data between supported sources.

> **Status:** Pre-release. Source and binaries have not yet been published. This documentation describes the current development build; a public installer and portable package are planned.

Switch Save Assistant is an independent community project. It is **not affiliated with, endorsed by, or sponsored by Nintendo** or the developers of supported emulators.

[Leia em português](README.pt-BR.md)

## Supported sources

| Source | Current scope |
| --- | --- |
| Nintendo Switch via DBI/MTP | Save discovery and the Switch ↔ Folder workflow |
| Local Folder | User-selected save repository and comparison/synchronization endpoint |
| Eden emulator | Profile discovery and the Eden ↔ Folder workflow |
| Other emulators | Not implemented in the current release scope |

### What the application does

- Shows saves from the supported sources, grouped by game and player/profile when possible.
- Compares the source and destination to help the user choose an operation.
- Supports explicit synchronization and related safety/restore workflows.
- Maps provider-specific identities to **logical players**. A Switch display name and an Eden native profile ID can belong to one logical player without having matching identifiers.
- Detects collisions when multiple Folder aliases resolve to one logical player and game. Different save contents require the user to select an entire version; divergent files are **not merged automatically**.
- Provides safety backups for supported destructive operations and alias reconciliation.
- Includes a desktop interface localized for Portuguese (Brazil), English, and Spanish.

> **Data safety:** Synchronization can overwrite save data on the selected destination. Inspect the comparison carefully, keep independent backups, and never assume the most recently modified file represents the best in-game progress. The application does not automatically resolve every possible conflict.

## Development requirements

- Windows (the desktop application uses WPF).
- .NET **10 SDK** for building from source.
- A compatible device/emulator setup is required to exercise the respective provider.

From the repository root, use PowerShell:

```powershell
dotnet restore .\SwitchSaveAssistant.slnx
dotnet build .\SwitchSaveAssistant.slnx --no-restore
dotnet test .\SwitchSaveAssistant.Tests\SwitchSaveAssistant.Tests.csproj --no-restore
dotnet run --project .\SwitchSaveAssistant.App\SwitchSaveAssistant.App.csproj
```

The latest documented local baseline before publishing was **71 passing tests, zero compiler warnings, and zero build errors**. This baseline does not replace manual integration tests with real devices and saves.

## Repository layout

| Project | Responsibility |
| --- | --- |
| `SwitchSaveAssistant.App` | WPF user interface and localization |
| `SwitchSaveAssistant.Core` | Shared domain logic and integrations |
| `SwitchSaveAssistant.Probe` | Developer diagnostic utility |
| `SwitchSaveAssistant.Tests` | Automated regression tests |

Read [Architecture](docs/ARCHITECTURE.md) for an overview.

## Local data and privacy

The application uses local state such as `settings.json` and `profiles.json` under `%LOCALAPPDATA%\SwitchSaveAssistant`. It also creates safety backups for applicable workflows. Your selected save Folder and emulator/device paths can be elsewhere.

**Do not add any of the following to commits, issues, or pull requests:** actual saves, profile databases, console identifiers, personal Windows paths, runtime settings, secrets, or local safety backups. Use synthetic test data whenever possible.

See [Security](SECURITY.md) and [Contribution guide](CONTRIBUTING.md).

## Current release plan

The current priority is final regression testing, public documentation, a portable Windows x64 package, a Windows installer, and GitHub Releases with checksums. Additional emulator providers are postponed.

## License

Switch Save Assistant is licensed under the [MIT License](LICENSE). Third-party components retain their respective licenses and notices. See [Third-party notices](THIRD-PARTY-NOTICES.md) and the [dependency inventory](docs/DEPENDENCIES.md).
