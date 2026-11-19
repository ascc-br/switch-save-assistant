using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Sync;

public sealed class ManifestComparisonService
{
    public ManifestComparisonResult Compare(
        string baselineManifestPath,
        string currentManifestPath
    )
    {
        return Compare(
            ReadManifest(baselineManifestPath),
            ReadManifest(currentManifestPath)
        );
    }

    public ManifestComparisonResult Compare(
        IReadOnlyDictionary<string, string> baseline,
        IReadOnlyDictionary<string, string> current
    )
    {
        var paths = baseline.Keys
            .Concat(current.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                path => path,
                StringComparer.Ordinal
            );

        var changes = new List<ManifestChange>();

        foreach (var path in paths)
        {
            var baselineExists = baseline.TryGetValue(
                path,
                out var baselineHash
            );

            var currentExists = current.TryGetValue(
                path,
                out var currentHash
            );

            if (!baselineExists && currentExists)
            {
                changes.Add(
                    new ManifestChange(
                        path,
                        ManifestChangeType.Added,
                        null,
                        currentHash
                    )
                );

                continue;
            }

            if (baselineExists && !currentExists)
            {
                changes.Add(
                    new ManifestChange(
                        path,
                        ManifestChangeType.Removed,
                        baselineHash,
                        null
                    )
                );

                continue;
            }

            if (!string.Equals(
                    baselineHash,
                    currentHash,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                changes.Add(
                    new ManifestChange(
                        path,
                        ManifestChangeType.Modified,
                        baselineHash,
                        currentHash
                    )
                );
            }
        }

        return new ManifestComparisonResult(
            changes.Count == 0,
            changes
        );
    }

    public IReadOnlyDictionary<string, string> ReadManifest(
        string path
    )
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Manifest não encontrado.",
                path
            );
        }

        var result = new Dictionary<string, string>(
            StringComparer.Ordinal
        );

        var lineNumber = 0;

        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;

            var line = rawLine.TrimStart('\uFEFF');

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Length < 67)
            {
                throw new FormatException(
                    $"Manifest inválido na linha {lineNumber}: linha muito curta."
                );
            }

            var hash = line[..64];

            if (!hash.All(Uri.IsHexDigit))
            {
                throw new FormatException(
                    $"Manifest inválido na linha {lineNumber}: SHA-256 inválido."
                );
            }

            if (line[64] != ' ' ||
                (line[65] != ' ' && line[65] != '*'))
            {
                throw new FormatException(
                    $"Manifest inválido na linha {lineNumber}: separador inválido."
                );
            }

            var relativePath = line[66..]
                .Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new FormatException(
                    $"Manifest inválido na linha {lineNumber}: caminho vazio."
                );
            }

            if (!result.TryAdd(
                    relativePath,
                    hash.ToLowerInvariant()
                ))
            {
                throw new FormatException(
                    $"Manifest contém caminho duplicado: '{relativePath}'."
                );
            }
        }

        return result;
    }
}
