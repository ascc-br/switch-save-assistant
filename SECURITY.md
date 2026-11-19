# Security policy (draft)

The project is being prepared for its first public release. The supported versions and confidential reporting channel will be finalized before publication.

## Scope of sensitive data

Switch Save Assistant may access locally stored game saves, emulator profile identifiers, selected source/destination paths, configuration files and safety backups. Treat these as private user data.

## Reporting vulnerabilities

**Do not publish exploitable vulnerabilities, confidential logs, personal identifiers or real saves in public issues.** A private reporting channel must be configured on the GitHub repository before launch (for example, GitHub's private vulnerability reporting feature). The final public policy will link directly to that channel.

For non-sensitive bugs, supply sanitized reproduction steps and synthetic input files only.

## Safe use

- Review source/destination selection before synchronization.
- Maintain independent backups of important saves.
- Do not execute unknown scripts or builds from untrusted sources.
- Do not publish `%LOCALAPPDATA%\SwitchSaveAssistant` contents or emulator/console profile databases.

## Supported versions

No public release is supported yet. This section will be updated with the first release.
