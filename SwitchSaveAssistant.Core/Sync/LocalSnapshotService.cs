using SwitchSaveAssistant.Core.Hashing;

namespace SwitchSaveAssistant.Core.Sync;

public sealed class LocalSnapshotService
{
    private readonly Sha256Service _sha256Service = new();

    public IReadOnlyDictionary<string, string> Create(
        string rootPath
    )
    {
        var fullRoot = Path.GetFullPath(rootPath);

        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException(
                $"Diretório local não encontrado: {fullRoot}"
            );
        }

        var result = new Dictionary<string, string>(
            StringComparer.Ordinal
        );

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var file in Directory
                     .EnumerateFiles(
                         fullRoot,
                         "*",
                         options
                     )
                     .OrderBy(
                         path => path,
                         StringComparer.Ordinal
                     ))
        {
            var relativePath = Path
                .GetRelativePath(
                    fullRoot,
                    file
                )
                .Replace('\\', '/');

            var sha256 = _sha256Service
                .CalculateFile(file);

            if (!result.TryAdd(
                    relativePath,
                    sha256
                ))
            {
                throw new InvalidOperationException(
                    $"Arquivo duplicado no snapshot: '{relativePath}'."
                );
            }
        }

        return result;
    }
}
