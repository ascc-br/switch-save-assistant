using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Tests;

public sealed class SyncEvaluationServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public SyncEvaluationServiceTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "SwitchSaveAssistant.Tests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(
            _tempDirectory
        );
    }

    [Fact]
    public void SamePcAndSwitchAsBaseline_IsSynchronized()
    {
        var baseline = WriteManifest(
            "baseline.txt",
            ('a', "save.dat")
        );

        var pc = WriteManifest(
            "pc.txt",
            ('a', "save.dat")
        );

        var switchManifest = WriteManifest(
            "switch.txt",
            ('a', "save.dat")
        );

        var state = CreateState(
            baseline
        );

        var result = new SyncEvaluationService()
            .Evaluate(
                state,
                pc,
                switchManifest
            );

        Assert.Equal(
            SyncStatus.Synchronized,
            result.Status
        );

        Assert.False(
            result.BaselineNeedsRefresh
        );
    }

    [Fact]
    public void OnlyPcChanged_ReturnsPcChanged()
    {
        var baseline = WriteManifest(
            "baseline.txt",
            ('a', "save.dat")
        );

        var pc = WriteManifest(
            "pc.txt",
            ('b', "save.dat")
        );

        var switchManifest = WriteManifest(
            "switch.txt",
            ('a', "save.dat")
        );

        var state = CreateState(
            baseline
        );

        var result = new SyncEvaluationService()
            .Evaluate(
                state,
                pc,
                switchManifest
            );

        Assert.Equal(
            SyncStatus.PcChanged,
            result.Status
        );
    }

    [Fact]
    public void OnlySwitchChanged_ReturnsSwitchChanged()
    {
        var baseline = WriteManifest(
            "baseline.txt",
            ('a', "save.dat")
        );

        var pc = WriteManifest(
            "pc.txt",
            ('a', "save.dat")
        );

        var switchManifest = WriteManifest(
            "switch.txt",
            ('b', "save.dat")
        );

        var state = CreateState(
            baseline
        );

        var result = new SyncEvaluationService()
            .Evaluate(
                state,
                pc,
                switchManifest
            );

        Assert.Equal(
            SyncStatus.SwitchChanged,
            result.Status
        );
    }

    [Fact]
    public void PcAndSwitchChangedDifferently_ReturnsConflict()
    {
        var baseline = WriteManifest(
            "baseline.txt",
            ('a', "save.dat")
        );

        var pc = WriteManifest(
            "pc.txt",
            ('b', "save.dat")
        );

        var switchManifest = WriteManifest(
            "switch.txt",
            ('c', "save.dat")
        );

        var state = CreateState(
            baseline
        );

        var result = new SyncEvaluationService()
            .Evaluate(
                state,
                pc,
                switchManifest
            );

        Assert.Equal(
            SyncStatus.Conflict,
            result.Status
        );
    }

    [Fact]
    public void PcAndSwitchChangedToSameContent_IsSynchronized()
    {
        var baseline = WriteManifest(
            "baseline.txt",
            ('a', "save.dat")
        );

        var pc = WriteManifest(
            "pc.txt",
            ('b', "save.dat")
        );

        var switchManifest = WriteManifest(
            "switch.txt",
            ('b', "save.dat")
        );

        var state = CreateState(
            baseline
        );

        var result = new SyncEvaluationService()
            .Evaluate(
                state,
                pc,
                switchManifest
            );

        Assert.Equal(
            SyncStatus.Synchronized,
            result.Status
        );

        Assert.True(
            result.BaselineNeedsRefresh
        );
    }

    [Fact]
    public void SyncState_CanBeSavedAndLoaded()
    {
        var baseline = WriteManifest(
            "baseline.txt",
            ('a', "save.dat")
        );

        var service = new SyncStateService();

        var state = service.CreateFromManifest(
            "Installed games",
            "Mario Party Superstars",
            "Alfran",
            SaveEntryType.User,
            baseline
        );

        var path = Path.Combine(
            _tempDirectory,
            "sync-state.json"
        );

        service.Save(
            path,
            state
        );

        var loaded = service.Load(
            path
        );

        Assert.Equal(
            state.Category,
            loaded.Category
        );

        Assert.Equal(
            state.GameName,
            loaded.GameName
        );

        Assert.Equal(
            state.EntryName,
            loaded.EntryName
        );

        Assert.Equal(
            state.EntryType,
            loaded.EntryType
        );

        Assert.Single(
            loaded.BaselineFiles
        );

        Assert.Equal(
            new string('a', 64),
            loaded.BaselineFiles[0].Sha256
        );
    }

    private SyncState CreateState(
        string manifestPath
    )
    {
        return new SyncStateService()
            .CreateFromManifest(
                "Installed games",
                "Mario Party Superstars",
                "Alfran",
                SaveEntryType.User,
                manifestPath
            );
    }

    private string WriteManifest(
        string fileName,
        params (char HashCharacter, string Path)[] files
    )
    {
        var path = Path.Combine(
            _tempDirectory,
            fileName
        );

        var lines = files.Select(file =>
            $"{new string(file.HashCharacter, 64)}  {file.Path}"
        );

        File.WriteAllLines(
            path,
            lines
        );

        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(
                _tempDirectory,
                recursive: true
            );
        }
    }
}
