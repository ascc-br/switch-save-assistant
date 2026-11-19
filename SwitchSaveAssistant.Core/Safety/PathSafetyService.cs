namespace SwitchSaveAssistant.Core.Safety;

public static class PathSafetyService
{
    public static string ResolveChildPath(
        string rootPath,
        string relativePath
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootPath
        );

        ArgumentException.ThrowIfNullOrWhiteSpace(
            relativePath
        );

        var normalized = relativePath
            .Replace(
                '/',
                Path.DirectorySeparatorChar
            )
            .Replace(
                '\\',
                Path.DirectorySeparatorChar
            );

        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException(
                $"Caminho absoluto não permitido: '{relativePath}'."
            );
        }

        var segments = normalized.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries
        );

        if (segments.Any(segment =>
                segment == ".." ||
                segment == "."
            ))
        {
            throw new InvalidDataException(
                $"Caminho relativo inseguro: '{relativePath}'."
            );
        }

        var fullRoot = Path.GetFullPath(
            rootPath
        );

        var candidate = Path.GetFullPath(
            Path.Combine(
                fullRoot,
                normalized
            )
        );

        var rootPrefix =
            fullRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            )
            + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            throw new InvalidDataException(
                $"Caminho escaparia do diretório permitido: '{relativePath}'."
            );
        }

        return candidate;
    }
}
