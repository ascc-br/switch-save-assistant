using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Sync;

public sealed class PayloadValidationService
{
    private readonly LocalSnapshotService _snapshotService;
    private readonly ManifestComparisonService _comparisonService;

    public PayloadValidationService()
    {
        _snapshotService =
            new LocalSnapshotService();

        _comparisonService =
            new ManifestComparisonService();
    }

    public ManifestComparisonResult Validate(
        string payloadPath,
        string manifestPath
    )
    {
        var expected =
            _comparisonService.ReadManifest(
                manifestPath
            );

        var actual =
            _snapshotService.Create(
                payloadPath
            );

        return _comparisonService.Compare(
            expected,
            actual
        );
    }
}
