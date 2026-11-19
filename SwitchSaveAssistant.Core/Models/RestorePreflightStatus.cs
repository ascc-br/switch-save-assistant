namespace SwitchSaveAssistant.Core.Models;

public enum RestorePreflightStatus
{
    Ready,
    LocalPayloadInvalid,
    NoChanges,
    SwitchChanged,
    Conflict,
    SafetyBackupInvalid,
    SwitchChangedDuringPreflight
}
