using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Core.Emulators;

public enum EdenFolderSyncDirection
{
	EdenToFolder,
	FolderToEden
}

public sealed record EdenFolderSyncChange(
	string Operation,
	string RelativePath
);

public sealed record EdenFolderSyncPlan(
	EdenFolderSyncDirection Direction,
	string GameName,
	string ProfileName,
	string SourcePath,
	string DestinationPath,
	bool DestinationExists,
	IReadOnlyList<EdenFolderSyncChange> Changes,
	int SourceFileCount,
	int DestinationFileCount
);

public sealed record EdenFolderSyncResult(
	string DestinationPath,
	string? BackupPath,
	int FileCount
);

public sealed class EdenFolderSyncService
{
	private readonly LocalSnapshotService
		_snapshotService =
			new();

	private readonly ManifestComparisonService
		_comparisonService =
			new();

	public EdenFolderSyncPlan BuildPlan(
		EdenFolderSyncDirection direction,
		string gameName,
		string profileName,
		string edenPath,
		string folderPath
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(
			gameName
		);

		ArgumentException.ThrowIfNullOrWhiteSpace(
			profileName
		);

		var sourcePath =
			direction ==
			EdenFolderSyncDirection.EdenToFolder
				? edenPath
				: folderPath;

		var destinationPath =
			direction ==
			EdenFolderSyncDirection.EdenToFolder
				? folderPath
				: edenPath;

		if (!Directory.Exists(
				sourcePath
			))
		{
			throw new DirectoryNotFoundException(
				$"Origem não encontrada: {sourcePath}"
			);
		}

		var source =
			ReadComparableSnapshot(
				sourcePath
			);

		var destinationExists =
			Directory.Exists(
				destinationPath
			);

		var destination =
			destinationExists
				? ReadComparableSnapshot(
					destinationPath
				)
				: new Dictionary<string, string>(
					StringComparer.Ordinal
				);

		var changes =
			BuildChanges(
				source,
				destination
			);

		return new EdenFolderSyncPlan(
			direction,
			gameName,
			profileName,
			Path.GetFullPath(
				sourcePath
			),
			Path.GetFullPath(
				destinationPath
			),
			destinationExists,
			changes,
			source.Count,
			destination.Count
		);
	}

	public EdenFolderSyncResult Execute(
		EdenFolderSyncPlan plan,
		string backupRoot,
		Action<string>? progress = null
	)
	{
		ArgumentNullException.ThrowIfNull(
			plan
		);

		ArgumentException.ThrowIfNullOrWhiteSpace(
			backupRoot
		);

		if (!Directory.Exists(
				plan.SourcePath
			))
		{
			throw new DirectoryNotFoundException(
				$"Origem não encontrada: {plan.SourcePath}"
			);
		}

		progress?.Invoke(
			"Revalidando origem..."
		);

		var expectedSource =
			ReadComparableSnapshot(
				plan.SourcePath
			);

		var destinationParent =
			Directory.GetParent(
				plan.DestinationPath
			)
			?.FullName
			?? throw new InvalidOperationException(
				"Não foi possível determinar a pasta pai do destino."
			);

		Directory.CreateDirectory(
			destinationParent
		);

		string? backupPath =
			null;

		if (Directory.Exists(
			plan.DestinationPath
		))
		{
			progress?.Invoke(
				"Criando backup de segurança do destino..."
			);

			backupPath =
				CreateBackup(
					plan,
					backupRoot
				);
		}

		var stagingPath =
			Path.Combine(
				destinationParent,
				".ssa-staging-" +
				Guid.NewGuid()
					.ToString(
						"N"
					)
			);

		try
		{
			progress?.Invoke(
				"Copiando para staging..."
			);

			CopyComparableTree(
				plan.SourcePath,
				stagingPath
			);

			if (plan.Direction ==
					EdenFolderSyncDirection.FolderToEden &&
				Directory.Exists(
					plan.DestinationPath
				))
			{
				PreserveEdenTechnicalFile(
					plan.DestinationPath,
					stagingPath
				);
			}

			progress?.Invoke(
				"Validando staging por SHA-256..."
			);

			var stagedSnapshot =
				ReadComparableSnapshot(
					stagingPath
				);

			var stagedComparison =
				_comparisonService.Compare(
					expectedSource,
					stagedSnapshot
				);

			if (!stagedComparison.AreEqual)
			{
				throw new IOException(
					"O staging não corresponde à origem. Nenhuma substituição foi realizada."
				);
			}

			progress?.Invoke(
				"Substituindo destino..."
			);

			if (Directory.Exists(
				plan.DestinationPath
			))
			{
				Directory.Delete(
					plan.DestinationPath,
					recursive: true
				);
			}

			Directory.Move(
				stagingPath,
				plan.DestinationPath
			);

			progress?.Invoke(
				"Verificando resultado final..."
			);

			var finalSnapshot =
				ReadComparableSnapshot(
					plan.DestinationPath
				);

			var finalComparison =
				_comparisonService.Compare(
					expectedSource,
					finalSnapshot
				);

			if (!finalComparison.AreEqual)
			{
				throw new IOException(
					"A cópia terminou, mas a verificação SHA-256 final falhou. " +
					"O backup de segurança foi preservado."
				);
			}

			return new EdenFolderSyncResult(
				plan.DestinationPath,
				backupPath,
				finalSnapshot.Count
			);
		}
		finally
		{
			if (Directory.Exists(
				stagingPath
			))
			{
				try
				{
					Directory.Delete(
						stagingPath,
						recursive: true
					);
				}
				catch
				{
					// Preserva o erro principal.
				}
			}
		}
	}

	private string CreateBackup(
		EdenFolderSyncPlan plan,
		string backupRoot
	)
	{
		var timestamp =
			DateTime.Now.ToString(
				"yyyyMMdd-HHmmss-fff"
			);

		var backupPath =
			Path.Combine(
				Path.GetFullPath(
					backupRoot
				),
				timestamp +
					"-" +
					Guid.NewGuid()
						.ToString(
							"N"
						)[..8],
				SanitizePathPart(
					plan.GameName
				),
				SanitizePathPart(
					plan.ProfileName
				),
				"destination"
			);

		CopyFullTree(
			plan.DestinationPath,
			backupPath
		);

		var original =
			_snapshotService.Create(
				plan.DestinationPath
			);

		var backup =
			_snapshotService.Create(
				backupPath
			);

		var comparison =
			_comparisonService.Compare(
				original,
				backup
			);

		if (!comparison.AreEqual)
		{
			throw new IOException(
				"Falha ao validar o backup de segurança. A sincronização foi cancelada."
			);
		}

		return backupPath;
	}

	private static IReadOnlyDictionary<string, string>
		ReadComparableSnapshot(
			string path
		)
	{
		var snapshot =
			new LocalSnapshotService()
				.Create(
					path
				);

		return snapshot
			.Where(
				pair =>
					!IsTechnicalFile(
						pair.Key
					)
			)
			.ToDictionary(
				pair =>
					pair.Key,
				pair =>
					pair.Value,
				StringComparer.Ordinal
			);
	}

	private static IReadOnlyList<EdenFolderSyncChange>
		BuildChanges(
			IReadOnlyDictionary<string, string> source,
			IReadOnlyDictionary<string, string> destination
		)
	{
		var paths =
			source.Keys
				.Concat(
					destination.Keys
				)
				.Distinct(
					StringComparer.Ordinal
				)
				.OrderBy(
					path =>
						path,
					StringComparer.Ordinal
				)
				.ToArray();

		var result =
			new List<EdenFolderSyncChange>();

		foreach (var path
			in paths)
		{
			var sourceHas =
				source.TryGetValue(
					path,
					out var sourceHash
				);

			var destinationHas =
				destination.TryGetValue(
					path,
					out var destinationHash
				);

			if (sourceHas &&
				!destinationHas)
			{
				result.Add(
					new EdenFolderSyncChange(
						"Adicionar",
						path
					)
				);

				continue;
			}

			if (!sourceHas &&
				destinationHas)
			{
				result.Add(
					new EdenFolderSyncChange(
						"Remover",
						path
					)
				);

				continue;
			}

			if (!string.Equals(
				sourceHash,
				destinationHash,
				StringComparison.OrdinalIgnoreCase
			))
			{
				result.Add(
					new EdenFolderSyncChange(
						"Alterar",
						path
					)
				);
			}
		}

		return result;
	}

	private static void CopyComparableTree(
		string source,
		string destination
	)
	{
		Directory.CreateDirectory(
			destination
		);

		foreach (var directory
			in Directory.EnumerateDirectories(
				source,
				"*",
				SearchOption.AllDirectories
			))
		{
			var relative =
				Path.GetRelativePath(
					source,
					directory
				);

			Directory.CreateDirectory(
				Path.Combine(
					destination,
					relative
				)
			);
		}

		foreach (var file
			in Directory.EnumerateFiles(
				source,
				"*",
				SearchOption.AllDirectories
			))
		{
			var relative =
				Path.GetRelativePath(
					source,
					file
				);

			if (IsTechnicalFile(
				relative
			))
			{
				continue;
			}

			var target =
				Path.Combine(
					destination,
					relative
				);

			var targetDirectory =
				Path.GetDirectoryName(
					target
				);

			if (!string.IsNullOrWhiteSpace(
				targetDirectory
			))
			{
				Directory.CreateDirectory(
					targetDirectory
				);
			}

			File.Copy(
				file,
				target,
				overwrite: true
			);
		}
	}

	private static void CopyFullTree(
		string source,
		string destination
	)
	{
		Directory.CreateDirectory(
			destination
		);

		foreach (var directory
			in Directory.EnumerateDirectories(
				source,
				"*",
				SearchOption.AllDirectories
			))
		{
			var relative =
				Path.GetRelativePath(
					source,
					directory
				);

			Directory.CreateDirectory(
				Path.Combine(
					destination,
					relative
				)
			);
		}

		foreach (var file
			in Directory.EnumerateFiles(
				source,
				"*",
				SearchOption.AllDirectories
			))
		{
			var relative =
				Path.GetRelativePath(
					source,
					file
				);

			var target =
				Path.Combine(
					destination,
					relative
				);

			var targetDirectory =
				Path.GetDirectoryName(
					target
				);

			if (!string.IsNullOrWhiteSpace(
				targetDirectory
			))
			{
				Directory.CreateDirectory(
					targetDirectory
				);
			}

			File.Copy(
				file,
				target,
				overwrite: true
			);
		}
	}

	private static void PreserveEdenTechnicalFile(
		string currentDestination,
		string stagingPath
	)
	{
		var technicalFile =
			Path.Combine(
				currentDestination,
				".yuzu_save_size"
			);

		if (!File.Exists(
				technicalFile
			))
		{
			return;
		}

		File.Copy(
			technicalFile,
			Path.Combine(
				stagingPath,
				".yuzu_save_size"
			),
			overwrite: true
		);
	}

	private static bool IsTechnicalFile(
		string relativePath
	)
	{
		return string.Equals(
			Path.GetFileName(
				relativePath
			),
			".yuzu_save_size",
			StringComparison.OrdinalIgnoreCase
		);
	}

	private static string SanitizePathPart(
		string value
	)
	{
		var invalid =
			Path.GetInvalidFileNameChars();

		var result =
			new string(
				value
					.Select(
						character =>
							invalid.Contains(
								character
							)
								? '_'
								: character
					)
					.ToArray()
			)
			.Trim();

		return string.IsNullOrWhiteSpace(
			result
		)
			? "_"
			: result;
	}
}
