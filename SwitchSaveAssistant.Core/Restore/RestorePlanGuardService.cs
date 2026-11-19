using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Restore;

public sealed class RestorePlanGuardService
{
    private static readonly HashSet<string> AllowedCategories =
        new(
            new[]
            {
                "Installed games",
                "Uninstalled games"
            },
            StringComparer.Ordinal
        );

    public void Validate(
        RestorePlan plan
    )
    {
        ArgumentNullException.ThrowIfNull(
            plan
        );

        if (!AllowedCategories.Contains(
                plan.Category
            ))
        {
            throw new InvalidOperationException(
                $"Categoria de save não permitida: '{plan.Category}'."
            );
        }

        ValidateSegment(
            plan.Category,
            nameof(plan.Category)
        );

        ValidateSegment(
            plan.GameName,
            nameof(plan.GameName)
        );

        ValidateSegment(
            plan.EntryName,
            nameof(plan.EntryName)
        );

        if (plan.EntryType ==
            SaveEntryType.Unknown)
        {
            throw new InvalidOperationException(
                "RestorePlan possui EntryType Unknown."
            );
        }

        RequireAbsolutePath(
            plan.LocalPayloadPath,
            nameof(plan.LocalPayloadPath)
        );

        RequireAbsolutePath(
            plan.LocalManifestPath,
            nameof(plan.LocalManifestPath)
        );

        RequireAbsolutePath(
            plan.SafetyBackupPath,
            nameof(plan.SafetyBackupPath)
        );

        RequireAbsolutePath(
            plan.SafetyBackupManifestPath,
            nameof(plan.SafetyBackupManifestPath)
        );

        var seen =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (var file
                 in plan.ExpectedSwitchFiles)
        {
            ValidateRelativePath(
                file.RelativePath
            );

            ValidateSha256(
                file.Sha256
            );

            if (!seen.Add(
                    file.RelativePath
                ))
            {
                throw new InvalidOperationException(
                    $"Arquivo duplicado em ExpectedSwitchFiles: '{file.RelativePath}'."
                );
            }
        }
    }

    public IReadOnlyDictionary<string, string>
        CreateExpectedSwitchSnapshot(
            RestorePlan plan
        )
    {
        Validate(
            plan
        );

        return plan.ExpectedSwitchFiles
            .ToDictionary(
                file => file.RelativePath,
                file => file.Sha256,
                StringComparer.Ordinal
            );
    }

    public bool HasSameFileSet(
        IReadOnlyDictionary<string, string> target,
        IReadOnlyDictionary<string, string> current
    )
    {
        ArgumentNullException.ThrowIfNull(
            target
        );

        ArgumentNullException.ThrowIfNull(
            current
        );

        if (target.Count !=
            current.Count)
        {
            return false;
        }

        return target.Keys
            .OrderBy(
                path => path,
                StringComparer.Ordinal
            )
            .SequenceEqual(
                current.Keys.OrderBy(
                    path => path,
                    StringComparer.Ordinal
                ),
                StringComparer.Ordinal
            );
    }

    private static void ValidateSegment(
        string value,
        string name
    )
    {
        if (string.IsNullOrWhiteSpace(
                value
            ))
        {
            throw new InvalidOperationException(
                $"{name} não pode ser vazio."
            );
        }

        if (value is "." or "..")
        {
            throw new InvalidOperationException(
                $"{name} contém segmento inválido."
            );
        }

        if (value.Contains('\\') ||
            value.Contains('/'))
        {
            throw new InvalidOperationException(
                $"{name} não pode conter separadores de caminho."
            );
        }

        if (value.EndsWith(' ') ||
            value.EndsWith('.'))
        {
            throw new InvalidOperationException(
                $"{name} não pode terminar em espaço ou ponto."
            );
        }

        if (value.IndexOfAny(
                Path.GetInvalidFileNameChars()
            ) >= 0)
        {
            throw new InvalidOperationException(
                $"{name} contém caractere inválido para staging local."
            );
        }
    }

    private static void RequireAbsolutePath(
        string value,
        string name
    )
    {
        if (string.IsNullOrWhiteSpace(
                value
            ) ||
            !Path.IsPathFullyQualified(
                value
            ))
        {
            throw new InvalidOperationException(
                $"{name} deve ser um caminho absoluto."
            );
        }
    }

    private static void ValidateRelativePath(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(
                value
            ))
        {
            throw new InvalidOperationException(
                "RelativePath vazio em ExpectedSwitchFiles."
            );
        }

        var normalized =
            value.Replace(
                '\\',
                '/'
            );

        if (normalized.StartsWith('/') ||
            Path.IsPathRooted(value))
        {
            throw new InvalidOperationException(
                $"Caminho relativo inválido: '{value}'."
            );
        }

        foreach (var segment
                 in normalized.Split('/'))
        {
            if (string.IsNullOrEmpty(segment) ||
                segment is "." or "..")
            {
                throw new InvalidOperationException(
                    $"Caminho relativo inválido: '{value}'."
                );
            }

            if (segment.EndsWith(' ') ||
                segment.EndsWith('.') ||
                segment.IndexOfAny(
                    Path.GetInvalidFileNameChars()
                ) >= 0)
            {
                throw new InvalidOperationException(
                    $"Segmento inválido em caminho relativo: '{value}'."
                );
            }
        }
    }

    private static void ValidateSha256(
        string value
    )
    {
        if (value.Length != 64 ||
            value.Any(character =>
                !Uri.IsHexDigit(
                    character
                )
            ))
        {
            throw new InvalidOperationException(
                $"SHA-256 inválido: '{value}'."
            );
        }
    }
}
