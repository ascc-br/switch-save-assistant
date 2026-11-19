using System.Text.Json;
using SwitchSaveAssistant.Core.Emulators;

namespace SwitchSaveAssistant.Core.Backups;

public sealed record SafetyBackupMetadata
{
	public int Version
	{
		get;
		init;
	} =
		1;

	public required string BackupId
	{
		get;
		init;
	}

	public required DateTimeOffset CreatedAtUtc
	{
		get;
		init;
	}

	public required string GameName
	{
		get;
		init;
	}

	public required string ProfileName
	{
		get;
		init;
	}

	public required string Direction
	{
		get;
		init;
	}

	public required string DestinationSide
	{
		get;
		init;
	}

	public required string OriginalDestinationPath
	{
		get;
		init;
	}

	public required string BackupPayloadRelativePath
	{
		get;
		init;
	}

	public int FileCount
	{
		get;
		init;
	}
}

public sealed record SafetyBackupEntry(
	string BackupId,
	DateTimeOffset CreatedAtUtc,
	string GameName,
	string ProfileName,
	string Direction,
	string DestinationSide,
	string? OriginalDestinationPath,
	string BackupPayloadPath,
	string ContainerPath,
	int FileCount,
	long TotalBytes,
	bool IsRestorable,
	string Status
);

public sealed class SafetyBackupCatalogService
{
	private const string MetadataFileName =
		"metadata.json";

	private static readonly JsonSerializerOptions
		JsonOptions =
			new()
				{
					WriteIndented =
						true
				};

	public string DefaultRoot =>
		Path.Combine(
			Environment.GetFolderPath(
				Environment.SpecialFolder.LocalApplicationData
			),
			"SwitchSaveAssistant",
			"SafetyBackups",
			"EdenFolder"
		);

	public SafetyBackupEntry? Register(
		EdenFolderSyncPlan plan,
		EdenFolderSyncResult result
	)
	{
		ArgumentNullException.ThrowIfNull(
			plan
		);

		ArgumentNullException.ThrowIfNull(
			result
		);

		if (string.IsNullOrWhiteSpace(
				result.BackupPath
			) ||
			!Directory.Exists(
				result.BackupPath
			))
		{
			return null;
		}

		var container =
			FindBackupContainer(
				result.BackupPath
			);

		var metadata =
			new SafetyBackupMetadata
				{
					BackupId =
						Path.GetFileName(
							container
						),

					CreatedAtUtc =
						DateTimeOffset.UtcNow,

					GameName =
						plan.GameName,

					ProfileName =
						plan.ProfileName,

					Direction =
						plan.Direction.ToString(),

					DestinationSide =
						plan.Direction ==
							EdenFolderSyncDirection.EdenToFolder
							? "Folder"
							: "Eden",

					OriginalDestinationPath =
						plan.DestinationPath,

					BackupPayloadRelativePath =
						Path.GetRelativePath(
							container,
							result.BackupPath
						),

					FileCount =
						result.FileCount
				};

		WriteMetadata(
			container,
			metadata
		);

		return CreateEntry(
			container,
			metadata
		);
	}

	public IReadOnlyList<SafetyBackupEntry>
		List(
			string? root = null
		)
	{
		var effectiveRoot =
			Path.GetFullPath(
				root ??
				DefaultRoot
			);

		if (!Directory.Exists(
				effectiveRoot
			))
		{
			return Array.Empty<SafetyBackupEntry>();
		}

		var result =
			new List<SafetyBackupEntry>();

		var knownContainers =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase
			);

		foreach (var metadataPath
			in Directory.EnumerateFiles(
				effectiveRoot,
				MetadataFileName,
				SearchOption.AllDirectories
			))
		{
			var container =
				Path.GetDirectoryName(
					metadataPath
				);

			if (string.IsNullOrWhiteSpace(
				container
			))
			{
				continue;
			}

			try
			{
				var metadata =
					JsonSerializer.Deserialize<SafetyBackupMetadata>(
						File.ReadAllText(
							metadataPath
						),
						JsonOptions
					);

				if (metadata is null)
				{
					continue;
				}

				var fullContainer =
					Path.GetFullPath(
						container
					);

				knownContainers.Add(
					fullContainer
				);

				result.Add(
					CreateEntry(
						fullContainer,
						metadata
					)
				);
			}
			catch
			{
				// Metadata inválida: não impede listar os demais backups.
			}
		}

		foreach (var destinationDirectory
			in Directory.EnumerateDirectories(
				effectiveRoot,
				"destination",
				SearchOption.AllDirectories
			))
		{
			string container;

			try
			{
				container =
					FindBackupContainer(
						destinationDirectory
					);
			}
			catch
			{
				continue;
			}

			if (knownContainers.Contains(
				container
			))
			{
				continue;
			}

			var profileDirectory =
				Directory.GetParent(
					destinationDirectory
				);

			var gameDirectory =
				profileDirectory
					?.Parent;

			if (profileDirectory is null ||
				gameDirectory is null)
			{
				continue;
			}

			var stats =
				GetDirectoryStats(
					destinationDirectory
				);

			var containerInfo =
				new DirectoryInfo(
					container
				);

			result.Add(
				new SafetyBackupEntry(
					containerInfo.Name,
					containerInfo.LastWriteTimeUtc,
					gameDirectory.Name,
					profileDirectory.Name,
					"Desconhecida",
					"Legado",
					null,
					destinationDirectory,
					container,
					stats.FileCount,
					stats.TotalBytes,
					false,
					"Legado • somente consulta"
				)
			);
		}

		return result
			.OrderByDescending(
				entry =>
					entry.CreatedAtUtc
			)
			.ToArray();
	}

	private static void WriteMetadata(
		string container,
		SafetyBackupMetadata metadata
	)
	{
		var metadataPath =
			Path.Combine(
				container,
				MetadataFileName
			);

		var temporaryPath =
			metadataPath +
			".tmp";

		var json =
			JsonSerializer.Serialize(
				metadata,
				JsonOptions
			);

		File.WriteAllText(
			temporaryPath,
			json
		);

		File.Move(
			temporaryPath,
			metadataPath,
			overwrite: true
		);
	}

	private static SafetyBackupEntry CreateEntry(
		string container,
		SafetyBackupMetadata metadata
	)
	{
		var payloadPath =
			Path.GetFullPath(
				Path.Combine(
					container,
					metadata.BackupPayloadRelativePath
				)
			);

		var payloadExists =
			Directory.Exists(
				payloadPath
			);

		var destinationKnown =
			!string.IsNullOrWhiteSpace(
				metadata.OriginalDestinationPath
			);

		var stats =
			payloadExists
				? GetDirectoryStats(
					payloadPath
				)
				: (0, 0L);

		var restorable =
			payloadExists &&
			destinationKnown;

		return new SafetyBackupEntry(
			metadata.BackupId,
			metadata.CreatedAtUtc,
			metadata.GameName,
			metadata.ProfileName,
			metadata.Direction,
			metadata.DestinationSide,
			metadata.OriginalDestinationPath,
			payloadPath,
			container,
			stats.Item1,
			stats.Item2,
			restorable,
			restorable
				? "Restaurável"
				: "Incompleto"
		);
	}

	private static string FindBackupContainer(
		string backupPayloadPath
	)
	{
		var current =
			new DirectoryInfo(
				Path.GetFullPath(
					backupPayloadPath
				)
			);

		for (var index = 0;
			index < 3;
			index++)
		{
			current =
				current.Parent ??
				throw new InvalidOperationException(
					"Estrutura de backup inesperada."
				);
		}

		return current.FullName;
	}

	private static (int FileCount, long TotalBytes)
		GetDirectoryStats(
			string path
		)
	{
		var count =
			0;

		var total =
			0L;

		try
		{
			foreach (var file
				in Directory.EnumerateFiles(
					path,
					"*",
					SearchOption.AllDirectories
				))
			{
				try
				{
					count++;

					total +=
						new FileInfo(
							file
						)
						.Length;
				}
				catch
				{
				}
			}
		}
		catch
		{
		}

		return (
			count,
			total
		);
	}
}
