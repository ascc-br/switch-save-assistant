using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Restore;

namespace SwitchSaveAssistant.Tests;

public sealed class RestorePolicyServiceTests
{
    [Theory]
    [InlineData(
        SyncStatus.PcChanged,
        RestorePreflightStatus.Ready
    )]
    [InlineData(
        SyncStatus.Synchronized,
        RestorePreflightStatus.NoChanges
    )]
    [InlineData(
        SyncStatus.SwitchChanged,
        RestorePreflightStatus.SwitchChanged
    )]
    [InlineData(
        SyncStatus.Conflict,
        RestorePreflightStatus.Conflict
    )]
    public void Status_IsMappedToSafeRestoreDecision(
        SyncStatus syncStatus,
        RestorePreflightStatus expected
    )
    {
        var equal = new ManifestComparisonResult(
            true,
            Array.Empty<ManifestChange>()
        );

        var evaluation = new SyncEvaluationResult(
            syncStatus,
            false,
            equal,
            equal,
            equal
        );

        var result =
            new RestorePolicyService()
                .Decide(
                    evaluation
                );

        Assert.Equal(
            expected,
            result
        );
    }
}
