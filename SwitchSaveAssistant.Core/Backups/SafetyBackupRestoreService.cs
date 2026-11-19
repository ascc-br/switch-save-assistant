using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Core.Backups;

public sealed record SafetyBackupRestoreChange(
	string Operation,
	string RelativePath
);

public sealed record SafetyBackupRestorePlan(
	SafetyBackupEntry Backup,
	IReadOnlyList<SafetyBackupRestoreChange> Changes,
	bool DestinationExists
);

public sealed record SafetyBackupRestoreResult(
	string DestinationPath,
	string? RollbackPath,
	int FileCount
);

public sealed class SafetyBackupRestoreService
{
	private readonly LocalSnapshotService
		_snapshotService =
			new();

	private readonly ManifestComparisonService
		_comparisonService =
			new();

	public SafetyBackupRestorePlan BuildPlan(
		SafetyBackupEntry backup
	)
	{
		ArgumentNullException.ThrowIfNull(
			backup
		);

		if (!backup.IsRestorable ||
			string.IsNullOrWhiteSpace(
				backup.OriginalDestinationPath
			))
		{
			throw new InvalidOperationException(
				"Este backup não contém metadados suficientes para restauração automática."
			);
		}

		if (!Directory.Exists(
				backup.BackupPayloadPath
			))
		{
			throw new DirectoryNotFoundException(
				$"Payload do backup não encontrado: {backup.BackupPayloadPath}"
			);
		}

		var source =
			_snapshotService.Create(
				backup.BackupPayloadPath
			);

		var destinationExists =
			Directory.Exists(
				backup.OriginalDestinationPath
			);

		var destination =
			destinationExists
				? _snapshotService.Create(
					backup.OriginalDestinationPath
				)
				: new Dictionary<string, string>(
					StringComparer.Ordinal
				);

		return new SafetyBackupRestorePlan(
			backup,
			BuildChanges(
				source,
				destination
			),
			destinationExists
		);
	}

	public SafetyBackupRestoreResult Execute(
		SafetyBackupRestorePlan plan,
		string rollbackRoot,
		Action<string>? progress = null
	)
	{
		ArgumentNullException.ThrowIfNull(
			plan
		);

		ArgumentException.ThrowIfNullOrWhiteSpace(
			rollbackRoot
		);

		var backup =
			plan.Backup;

		if (string.IsNullOrWhiteSpace(
				backup.OriginalDestinationPath
			))
		{
			throw new InvalidOperationException(
				"Destino original ausente."
			);
		}

		var destination =
			Path.GetFullPath(
				backup.OriginalDestinationPath
			);

		var sourceSnapshot =
			_snapshotService.Create(
				backup.BackupPayloadPath
			);

		var parent =
			Directory.GetParent(
				destination
			)
			?.FullName ??
			throw new InvalidOperationException(
				"Não foi possível determinar a pasta pai do destino."
			);

		Directory.CreateDirectory(
			parent
		);

		string? rollbackPath =
			null;

		if (Directory.Exists(
				destination
			))
		{
			progress?.Invoke(
				"Criando rollback do estado atual..."
			);

			rollbackPath =
				CreateRollback(
					backup,
					destination,
					rollbackRoot
				);
		}

		var staging =
			Path.Combine(
				parent,
				".ssa-restore-staging-" +
				Guid.NewGuid()
					.ToString(
						"N"
					)
			);

		var destinationTouched =
			false;

		try
		{
			progress?.Invoke(
				"Copiando backup para staging..."
			);

			CopyFullTree(
				backup.BackupPayloadPath,
				staging
			);

			progress?.Invoke(
				"Validando staging por SHA-256..."
			);

			EnsureEqual(
				sourceSnapshot,
				_snapshotService.Create(
					staging
				),
				"O staging não corresponde ao backup."
			);

			progress?.Invoke(
				"Substituindo destino..."
			);

			if (Directory.Exists(
				destination
			))
			{
				Directory.Delete(
					destination,
					recursive: true
				);
			}

			destinationTouched =
				true;

			Directory.Move(
				staging,
				destination
			);

			progress?.Invoke(
				"Validando restauração final..."
			);

			var finalSnapshot =
				_snapshotService.Create(
					destination
				);

			EnsureEqual(
				sourceSnapshot,
				finalSnapshot,
				"A restauração terminou, mas a verificação SHA-256 final falhou."
			);

			return new SafetyBackupRestoreResult(
				destination,
				rollbackPath,
				finalSnapshot.Count
			);
		}
		catch
		{
			if (destinationTouched &&
				!string.IsNullOrWhiteSpace(
					rollbackPath
				) &&
				Directory.Exists(
					rollbackPath
				))
			{
				try
				{
					progress?.Invoke(
						"Falha detectada; restaurando rollback..."
					);

					if (Directory.Exists(
						destination
					))
					{
						Directory.Delete(
							destination,
							recursive: true
						);
					}

					CopyFullTree(
						rollbackPath,
						destination
					);

					EnsureEqual(
						_snapshotService.Create(
							rollbackPath
						),
						_snapshotService.Create(
							destination
						),
						"Falha também ao validar o rollback."
					);
				}
				catch
				{
					// Mantém a exceção original; rollback permanece no disco.
				}
			}

			throw;
		}
		finally
		{
			if (Directory.Exists(
				staging
			))
			{
				try
				{
					Directory.Delete(
						staging,
						recursive: true
					);
				}
				catch
				{
				}
			}
		}
	}

	private string CreateRollback(
		SafetyBackupEntry backup,
		string destination,
		string rollbackRoot
	)
	{
		var rollbackPath =
			Path.Combine(
				Path.GetFullPath(
					rollbackRoot
				),
				DateTime.Now.ToString(
					"yyyyMMdd-HHmmss-fff"
				) +
				"-" +
				Guid.NewGuid()
					.ToString(
						"N"
					)
					[..8],
				Sanitize(
					backup.GameName
				),
				Sanitize(
					backup.ProfileName
				),
				"destination"
			);

		CopyFullTree(
			destination,
				rollbackPath
			);

		EnsureEqual(
			_snapshotService.Create(
				destination
			),
			_snapshotService.Create(
				rollbackPath
			),
			"Falha ao validar o rollback. A restauração foi cancelada."
		);

		return rollbackPath;
	}

	private void EnsureEqual(
		IReadOnlyDictionary<string, string> expected,
		IReadOnlyDictionary<string, string> actual,
		string message
	)
	{
		var comparison =
			_comparisonService.Compare(
				expected,
					actual
			);

		if (!comparison.AreEqual)
		{
			throw new IOException(
				message
			);
		}
	}

	private static IReadOnlyList<SafetyBackupRestoreChange>
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
				);

		var result =
			new List<SafetyBackupRestoreChange>();

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
					new SafetyBackupRestoreChange(
						"Adicionar",
						path
					)
				);
			}
			else if (!sourceHas &&
				destinationHas)
			{
				result.Add(
					new SafetyBackupRestoreChange(
						"Remover",
						path
					)
				);
			}
			else if (!string.Equals(
				sourceHash,
				destinationHash,
				StringComparison.OrdinalIgnoreCase
			))
			{
				result.Add(
					new SafetyBackupRestoreChange(
						"Alterar",
						path
					)
				);
			}
		}

		return result;
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

	private static string Sanitize(
		string value
	)
	{
		var invalid =
			Path.GetInvalidFileNameChars();

		return new string(
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
		);
	}
}
