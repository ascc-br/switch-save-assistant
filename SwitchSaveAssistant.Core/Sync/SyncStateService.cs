using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Sync;

public sealed class SyncStateService
{
    private const int CurrentSchemaVersion = 1;

    private readonly ManifestComparisonService _manifestService;

    private readonly JsonSerializerOptions _jsonOptions;

    public SyncStateService()
    {
        _manifestService = new ManifestComparisonService();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        _jsonOptions.Converters.Add(
            new JsonStringEnumConverter()
        );
    }

    public SyncState CreateFromManifest(
        string category,
        string gameName,
        string entryName,
        SaveEntryType entryType,
        string baselineManifestPath,
        DateTimeOffset? synchronizedAtUtc = null
    )
    {
        var manifest = _manifestService.ReadManifest(
            baselineManifestPath
        );

        var files = manifest
            .OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal
            )
            .Select(pair =>
                new SyncFileState(
                    pair.Key,
                    pair.Value
                )
            )
            .ToList();

        return new SyncState(
            CurrentSchemaVersion,
            category,
            gameName,
            entryName,
            entryType,
            synchronizedAtUtc ?? DateTimeOffset.UtcNow,
            files
        );
    }

    public void Save(
        string path,
        SyncState state
    )
    {
        ArgumentNullException.ThrowIfNull(state);

        var fullPath = Path.GetFullPath(path);

        var directory = Path.GetDirectoryName(
            fullPath
        );

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            state,
            _jsonOptions
        );

        var temporaryPath = fullPath + ".tmp";

        File.WriteAllText(
            temporaryPath,
            json + "\n",
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false
            )
        );

        File.Move(
            temporaryPath,
            fullPath,
            overwrite: true
        );
    }

    public SyncState Load(
        string path
    )
    {
        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "sync-state.json não encontrado.",
                fullPath
            );
        }

        var json = File.ReadAllText(
            fullPath,
            Encoding.UTF8
        );

        var state = JsonSerializer.Deserialize<SyncState>(
            json,
            _jsonOptions
        );

        if (state is null)
        {
            throw new FormatException(
                "sync-state.json inválido."
            );
        }

        if (state.SchemaVersion != CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Versão de sync-state não suportada: {state.SchemaVersion}."
            );
        }

        return state;
    }
}
