# Switch Save Assistant

## Download

**[Download the Windows x64 installer](https://github.com/ascc-br/switch-save-assistant/releases/download/v0.1.0-preview.2/SwitchSaveAssistant-Setup-0.1.0-preview.2-win-x64.exe)**

Latest available version: **0.1.0-preview.2 (pre-release)**.

[Portable ZIP](https://github.com/ascc-br/switch-save-assistant/releases/download/v0.1.0-preview.2/SwitchSaveAssistant-Portable-0.1.0-preview.2-win-x64.zip) · [Release notes and SHA-256 checksums](https://github.com/ascc-br/switch-save-assistant/releases/tag/v0.1.0-preview.2) · [All releases](https://github.com/ascc-br/switch-save-assistant/releases)

The packages include the .NET runtime. No SDK installation is required to run the application.

A Windows desktop application for inspecting, comparing, backing up and synchronizing Nintendo Switch save data between supported sources.

> **Status:** Public pre-release. Windows x64 installer and portable packages are available above.

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

The validated Windows Release build has **102 passing tests, zero compiler warnings, and zero build errors**. This baseline does not replace manual integration tests with real devices and saves.

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

Windows x64 installer and portable packages are published through GitHub Releases with SHA-256 checksums. The application can check for updates in Settings. The installer supports update/repair and uninstall, with optional removal of saved profiles and settings. Additional emulator providers are postponed.

## License

Switch Save Assistant is licensed under the [MIT License](LICENSE). Third-party components retain their respective licenses and notices. See [Third-party notices](THIRD-PARTY-NOTICES.md) and the [dependency inventory](docs/DEPENDENCIES.md).
