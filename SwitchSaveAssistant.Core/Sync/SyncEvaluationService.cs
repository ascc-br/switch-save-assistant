using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Sync;

public sealed class SyncEvaluationService
{
    private readonly ManifestComparisonService _manifestService;

    public SyncEvaluationService()
    {
        _manifestService = new ManifestComparisonService();
    }

    public SyncEvaluationResult Evaluate(
        SyncState state,
        string pcManifestPath,
        string switchManifestPath
    )
    {
        var pc = _manifestService.ReadManifest(
            pcManifestPath
        );

        var switchState = _manifestService.ReadManifest(
            switchManifestPath
        );

        return Evaluate(
            state,
            pc,
            switchState
        );
    }

    public SyncEvaluationResult Evaluate(
        SyncState state,
        IReadOnlyDictionary<string, string> pc,
        IReadOnlyDictionary<string, string> switchState
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pc);
        ArgumentNullException.ThrowIfNull(switchState);

        var baseline = state
            .BaselineFiles
            .ToDictionary(
                file => file.RelativePath,
                file => file.Sha256,
                StringComparer.Ordinal
            );

        var pcAgainstBaseline =
            _manifestService.Compare(
                baseline,
                pc
            );

        var switchAgainstBaseline =
            _manifestService.Compare(
                baseline,
                switchState
            );

        var pcAgainstSwitch =
            _manifestService.Compare(
                pc,
                switchState
            );

        SyncStatus status;

        if (pcAgainstSwitch.AreEqual)
        {
            status = SyncStatus.Synchronized;
        }
        else if (
            pcAgainstBaseline.AreEqual &&
            !switchAgainstBaseline.AreEqual
        )
        {
            status = SyncStatus.SwitchChanged;
        }
        else if (
            !pcAgainstBaseline.AreEqual &&
            switchAgainstBaseline.AreEqual
        )
        {
            status = SyncStatus.PcChanged;
        }
        else
        {
            status = SyncStatus.Conflict;
        }

        var baselineNeedsRefresh =
            status == SyncStatus.Synchronized &&
            (
                !pcAgainstBaseline.AreEqual ||
                !switchAgainstBaseline.AreEqual
            );

        return new SyncEvaluationResult(
            status,
            baselineNeedsRefresh,
            pcAgainstBaseline,
            switchAgainstBaseline,
            pcAgainstSwitch
        );
    }
}
