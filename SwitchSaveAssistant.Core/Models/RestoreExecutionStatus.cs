namespace SwitchSaveAssistant.Core.Models;

public enum RestoreExecutionStatus
{
    Completed,
    PlanInvalid,
    LocalPayloadInvalid,
    SafetyBackupInvalid,
    FileSetMismatch,
    RemoteIdentityMismatch,
    SwitchChangedSincePreflight,
    ShellCopyFailed,
    PostWriteVerificationFailed
}
