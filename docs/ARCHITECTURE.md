# Architecture overview

This document describes the current development architecture at a conceptual level. It does not replace source-level review before a public release.

## Projects

- **App:** Windows Presentation Foundation (WPF) desktop UI, localization resources and user workflows.
- **Core:** Reusable services for settings, provider discovery, comparisons, saves, profiles, backups and restore operations.
- **Probe:** Developer-oriented command-line diagnostics for provider behavior.
- **Tests:** Automated tests for domain services, configuration persistence and safety logic.

## Save identity

A provider's native identity is not the same thing as a logical person. The application can bind, for instance, an Eden stable native profile ID and a Switch display-name-only identity to one logical player.

```text
Logical player: Player A
    |-- Eden: stable native profile ID
    |-- Switch: display-name-only identity
    `-- Folder: linked aliases and canonical logical name
```

Comparisons should not assume that two identifiers from different providers match. Device/shared saves are separate from named user profiles.

## Folder alias collisions

Aliases may have been saved under separate directory names before their logical identities were linked. After linking, two directories for the same category/game/logical player can collide.

The conservative policy is:

1. Detect all aliases that now map to the same logical player within the same game/category.
2. If contents are identical, safe consolidation is possible.
3. If contents differ, require the user to choose which **entire** version to preserve.
4. Keep an appropriate safety backup/rollback path. Do **not** merge divergent save trees file by file.
5. If the user cancels, leave the conflict unresolved and prevent an unsafe synchronization for that entry.

## Data flow

```text
Switch / DBI-MTP -------\
                        +--> Discovery / comparison --> Preview --> User action
Eden profile storage ---/                 |                  |
                                         v                  v
                                  Logical player       Safety / restore
                                  resolution           services
                                         |
                                         v
                                Local Folder repository
```

Not all operations are supported for every source or save type. Source and destination must be reviewed before synchronization.

## Local state

App-managed state includes `%LOCALAPPDATA%\SwitchSaveAssistant\settings.json`, `profiles.json` and safety backup directories. A user-selected Folder repository is separate from source code. None of these runtime data files should be committed.

## Planned provider expansion

Support for additional emulators is deferred. Provider-specific paths and identity handling should be kept isolated behind services rather than assumed equivalent to Eden.
