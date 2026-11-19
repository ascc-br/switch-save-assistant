using SwitchSaveAssistant.Core.Sync;
using System.Text.Json;

namespace SwitchSaveAssistant.Core.Profiles;

public sealed record ProfileAliasCandidate(
	string Alias,
	string Path,
	int FileCount,
	long TotalBytes,
	DateTimeOffset? LastWriteTime
);

public sealed record ProfileAliasDifference(
	string ReferenceAlias,
	string ComparedAlias,
	string Kind,
	string RelativePath
);

public sealed record ProfileAliasRepairPlan(
	string FolderRoot,
	string Category,
	string GameName,
	string LogicalPlayerName,
	string CanonicalPath,
	IReadOnlyList<ProfileAliasCandidate> Candidates,
	bool AllIdentical,
	IReadOnlyList<ProfileAliasDifference> Differences
);

public enum ProfileAliasRepairStage
{
	CreatingBackup,
	ValidatingBackup,
	CreatingStaging,
	ValidatingStaging,
	ReplacingAliases,
	ValidatingFinal,
	RestoringRollback
}

public sealed record ProfileAliasRepairProgress(
	ProfileAliasRepairStage Stage,
	string? Alias = null
);

public sealed record ProfileAliasRepairResult(
	string CanonicalPath,
	string BackupPath,
	int FileCount
);

public sealed class ProfileAliasRepairService
{
	private readonly LocalSnapshotService
		_snapshotService =
			new();

	private static readonly
		JsonSerializerOptions
		JsonOptions =
			new()
				{
					WriteIndented =
						true
				};

	public static string GetDefaultBackupRoot()
	{
		return Path.Combine(
			Environment.GetFolderPath(
				Environment.SpecialFolder.LocalApplicationData
			),
			"SwitchSaveAssistant",
			"SafetyBackups",
			"ProfileAliasRepair"
		);
	}

	public ProfileAliasRepairPlan BuildPlan(
		string folderRoot,
		string category,
		string gameName,
		string logicalPlayerName,
		IEnumerable<string> aliases
	)
	{
		var safeFolderRoot =
			Path.GetFullPath(
				RequireDirectory(
					folderRoot,
					nameof(
						folderRoot
					)
				)
			);

		var safeCategory =
			RequireSegment(
				category,
				nameof(
					category
				)
			);

		var safeGameName =
			RequireSegment(
				gameName,
				nameof(
					gameName
				)
			);

		var safeLogicalName =
			RequireSegment(
				logicalPlayerName,
				nameof(
					logicalPlayerName
				)
			);

		var distinctAliases =
			aliases
				.Where(
					alias =>
						!string.IsNullOrWhiteSpace(
							alias
						)
				)
				.Select(
					alias =>
						RequireSegment(
							alias,
							nameof(
								aliases
							)
						)
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.OrderBy(
					alias =>
						alias,
					StringComparer.CurrentCultureIgnoreCase
				)
				.ToArray();

		if (distinctAliases.Length <
			2)
		{
			throw new InvalidOperationException(
				"At least two aliases are required to repair a conflict."
			);
		}

		var gameRoot =
			Path.Combine(
				safeFolderRoot,
				safeCategory,
				safeGameName
			);

		if (!Directory.Exists(
			gameRoot
		))
		{
			throw new DirectoryNotFoundException(
				gameRoot
			);
		}

		var canonicalPath =
			Path.Combine(
				gameRoot,
				safeLogicalName
			);

		if (
			Directory.Exists(
				canonicalPath
			) &&
			!distinctAliases.Contains(
				safeLogicalName,
				StringComparer.OrdinalIgnoreCase
			)
		)
		{
			throw new InvalidOperationException(
				"The canonical logical-player directory already exists but is not one of the conflict aliases."
			);
		}

		var candidates =
			new List<ProfileAliasCandidate>();

		var snapshots =
			new Dictionary<
				string,
				IReadOnlyDictionary<string, string>
			>(
				StringComparer.OrdinalIgnoreCase
			);

		foreach (var alias
			in distinctAliases)
		{
			var path =
				Path.Combine(
					gameRoot,
					alias
				);

			if (!Directory.Exists(
				path
			))
			{
				throw new DirectoryNotFoundException(
					path
				);
			}

			var snapshot =
				_snapshotService.Create(
					path
				);

			snapshots[alias] =
				snapshot;

			var stats =
				GetStats(
					path
				);

			candidates.Add(
				new ProfileAliasCandidate(
					alias,
					path,
					snapshot.Count,
					stats.TotalBytes,
					stats.LastWriteTime
				)
			);
		}

		var reference =
			candidates[0];

		var differences =
			new List<ProfileAliasDifference>();

		foreach (var candidate
			in candidates.Skip(
				1
			))
		{
			differences.AddRange(
				BuildDifferences(
					reference.Alias,
					snapshots[
						reference.Alias
					],
					candidate.Alias,
					snapshots[
						candidate.Alias
					]
				)
			);
		}

		return new ProfileAliasRepairPlan(
			safeFolderRoot,
			safeCategory,
			safeGameName,
			safeLogicalName,
			canonicalPath,
			candidates,
			differences.Count ==
				0,
			differences
		);
	}

	public ProfileAliasRepairResult Execute(
		ProfileAliasRepairPlan plan,
		string selectedAlias,
		string? backupRoot = null,
		Action<ProfileAliasRepairProgress>?
			progress = null
	)
	{
		ArgumentNullException.ThrowIfNull(
			plan
		);

		var selected =
			plan.Candidates.FirstOrDefault(
				candidate =>
					string.Equals(
						candidate.Alias,
						selectedAlias,
						StringComparison.OrdinalIgnoreCase
					)
			) ??
			throw new InvalidOperationException(
				"The selected alias is not part of this repair plan."
			);

		var currentPlan =
			BuildPlan(
				plan.FolderRoot,
				plan.Category,
				plan.GameName,
				plan.LogicalPlayerName,
				plan.Candidates.Select(
					candidate =>
						candidate.Alias
				)
			);

		selected =
			currentPlan.Candidates.First(
				candidate =>
					string.Equals(
						candidate.Alias,
						selectedAlias,
						StringComparison.OrdinalIgnoreCase
					)
			);

		var selectedSnapshot =
			_snapshotService.Create(
				selected.Path
			);

		var root =
			Path.GetFullPath(
				backupRoot ??
				GetDefaultBackupRoot()
			);

		var backupContainer =
			Path.Combine(
				root,
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
					currentPlan.Category
				),
				Sanitize(
					currentPlan.GameName
				),
				Sanitize(
					currentPlan.LogicalPlayerName
				)
			);

		var aliasBackupRoot =
			Path.Combine(
				backupContainer,
				"aliases"
			);

		var backupPaths =
			new Dictionary<string, string>(
				StringComparer.OrdinalIgnoreCase
			);

		Directory.CreateDirectory(
			aliasBackupRoot
		);

		for (var index = 0;
			index <
				currentPlan.Candidates.Count;
			index++)
		{
			var candidate =
				currentPlan.Candidates[
					index
				];

			progress?.Invoke(
				new ProfileAliasRepairProgress(
					ProfileAliasRepairStage.CreatingBackup,
					candidate.Alias
				)
			);

			var backupPath =
				Path.Combine(
					aliasBackupRoot,
					$"{index + 1:D2}-" +
					Sanitize(
						candidate.Alias
					)
				);

			CopyFullTree(
				candidate.Path,
				backupPath
			);

			progress?.Invoke(
				new ProfileAliasRepairProgress(
					ProfileAliasRepairStage.ValidatingBackup,
					candidate.Alias
				)
			);

			EnsureEqual(
				_snapshotService.Create(
					candidate.Path
				),
				_snapshotService.Create(
					backupPath
				),
				"Backup validation failed."
			);

			backupPaths[
				candidate.Alias
			] =
				backupPath;
		}

		var metadata =
			new
				{
					SchemaVersion =
						1,

					CreatedAt =
						DateTimeOffset.Now,

					currentPlan.Category,
					currentPlan.GameName,
					currentPlan.LogicalPlayerName,

					SelectedAlias =
						selected.Alias,

					currentPlan.CanonicalPath,

					Aliases =
						currentPlan.Candidates.Select(
							candidate =>
								candidate.Alias
						)
							.ToArray(),

					AllIdentical =
						currentPlan.AllIdentical
				};

		File.WriteAllText(
			Path.Combine(
				backupContainer,
				"metadata.json"
			),
			JsonSerializer.Serialize(
				metadata,
				JsonOptions
			)
		);

		var gameRoot =
			Directory.GetParent(
				currentPlan.CanonicalPath
			)
			?.FullName ??
			throw new InvalidOperationException(
				"Could not determine the game directory."
			);

		var staging =
			Path.Combine(
				gameRoot,
				".ssa-alias-repair-staging-" +
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
				new ProfileAliasRepairProgress(
					ProfileAliasRepairStage.CreatingStaging,
					selected.Alias
				)
			);

			CopyFullTree(
				selected.Path,
				staging
			);

			progress?.Invoke(
				new ProfileAliasRepairProgress(
					ProfileAliasRepairStage.ValidatingStaging,
					selected.Alias
				)
			);

			EnsureEqual(
				selectedSnapshot,
				_snapshotService.Create(
					staging
				),
				"Staging validation failed."
			);

			progress?.Invoke(
				new ProfileAliasRepairProgress(
					ProfileAliasRepairStage.ReplacingAliases
				)
			);

			destinationTouched =
				true;

			foreach (var candidate
				in currentPlan.Candidates)
			{
				if (Directory.Exists(
					candidate.Path
				))
				{
					Directory.Delete(
						candidate.Path,
						recursive: true
					);
				}
			}

			if (Directory.Exists(
				currentPlan.CanonicalPath
			))
			{
				Directory.Delete(
					currentPlan.CanonicalPath,
					recursive: true
				);
			}

			Directory.Move(
				staging,
				currentPlan.CanonicalPath
			);

			progress?.Invoke(
				new ProfileAliasRepairProgress(
					ProfileAliasRepairStage.ValidatingFinal
				)
			);

			var finalSnapshot =
				_snapshotService.Create(
					currentPlan.CanonicalPath
				);

			EnsureEqual(
				selectedSnapshot,
				finalSnapshot,
				"Final SHA-256 validation failed."
			);

			foreach (var candidate
				in currentPlan.Candidates)
			{
				if (
					string.Equals(
						candidate.Path,
						currentPlan.CanonicalPath,
						StringComparison.OrdinalIgnoreCase
					)
				)
				{
					continue;
				}

				if (Directory.Exists(
					candidate.Path
				))
				{
					throw new IOException(
						$"Alias directory still exists after repair: {candidate.Path}"
					);
				}
			}

			return new ProfileAliasRepairResult(
				currentPlan.CanonicalPath,
				backupContainer,
				finalSnapshot.Count
			);
		}
		catch
		{
			if (destinationTouched)
			{
				try
				{
					progress?.Invoke(
						new ProfileAliasRepairProgress(
							ProfileAliasRepairStage.RestoringRollback
						)
					);

					if (Directory.Exists(
						currentPlan.CanonicalPath
					))
					{
						Directory.Delete(
							currentPlan.CanonicalPath,
							recursive: true
						);
					}

					foreach (var candidate
						in currentPlan.Candidates)
					{
						if (Directory.Exists(
							candidate.Path
						))
						{
							Directory.Delete(
								candidate.Path,
								recursive: true
							);
						}

						CopyFullTree(
							backupPaths[
								candidate.Alias
							],
							candidate.Path
						);

						EnsureEqual(
							_snapshotService.Create(
								backupPaths[
									candidate.Alias
								]
							),
							_snapshotService.Create(
								candidate.Path
							),
							"Rollback validation failed."
						);
					}
				}
				catch
				{
					// The verified backup remains on disk for manual recovery.
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

	private static IReadOnlyList<ProfileAliasDifference>
		BuildDifferences(
			string referenceAlias,
			IReadOnlyDictionary<string, string>
				reference,
			string comparedAlias,
			IReadOnlyDictionary<string, string>
				compared
		)
	{
		var paths =
			reference.Keys
				.Concat(
					compared.Keys
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
			new List<ProfileAliasDifference>();

		foreach (var path
			in paths)
		{
			var referenceHas =
				reference.TryGetValue(
					path,
					out var referenceHash
				);

			var comparedHas =
				compared.TryGetValue(
					path,
					out var comparedHash
				);

			if (referenceHas &&
				!comparedHas)
			{
				result.Add(
					new ProfileAliasDifference(
						referenceAlias,
						comparedAlias,
						"OnlyReference",
						path
					)
				);
			}
			else if (!referenceHas &&
				comparedHas)
			{
				result.Add(
					new ProfileAliasDifference(
						referenceAlias,
						comparedAlias,
						"OnlyCompared",
						path
					)
				);
			}
			else if (!string.Equals(
				referenceHash,
				comparedHash,
				StringComparison.OrdinalIgnoreCase
			))
			{
				result.Add(
					new ProfileAliasDifference(
						referenceAlias,
						comparedAlias,
						"Changed",
						path
					)
				);
			}
		}

		return result;
	}

	private static void EnsureEqual(
		IReadOnlyDictionary<string, string>
			expected,
		IReadOnlyDictionary<string, string>
			actual,
		string message
	)
	{
		if (expected.Count !=
			actual.Count)
		{
			throw new IOException(
				message
			);
		}

		foreach (var pair
			in expected)
		{
			if (
				!actual.TryGetValue(
					pair.Key,
					out var actualHash
				) ||
				!string.Equals(
					pair.Value,
					actualHash,
					StringComparison.OrdinalIgnoreCase
				)
			)
			{
				throw new IOException(
					message
				);
			}
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

	private static (
		long TotalBytes,
		DateTimeOffset? LastWriteTime
	)
		GetStats(
			string path
		)
	{
		var totalBytes =
			0L;

		DateTimeOffset? lastWrite =
			null;

		foreach (var file
			in Directory.EnumerateFiles(
				path,
				"*",
				SearchOption.AllDirectories
			))
		{
			var info =
				new FileInfo(
					file
				);

			totalBytes +=
				info.Length;

			var current =
				new DateTimeOffset(
					info.LastWriteTimeUtc,
					TimeSpan.Zero
				);

			if (
				!lastWrite.HasValue ||
				current >
					lastWrite.Value
			)
			{
				lastWrite =
					current;
			}
		}

		return (
			totalBytes,
			lastWrite
		);
	}

	private static string RequireDirectory(
		string value,
		string parameterName
	)
	{
		if (string.IsNullOrWhiteSpace(
			value
		))
		{
			throw new ArgumentException(
				"Path cannot be empty.",
				parameterName
			);
		}

		var full =
			Path.GetFullPath(
				value
			);

		if (!Directory.Exists(
			full
		))
		{
			throw new DirectoryNotFoundException(
				full
			);
		}

		return full;
	}

	private static string RequireSegment(
		string value,
		string parameterName
	)
	{
		if (string.IsNullOrWhiteSpace(
			value
		))
		{
			throw new ArgumentException(
				"Directory name cannot be empty.",
				parameterName
			);
		}

		var trimmed =
			value.Trim();

		if (
			trimmed is "." or ".." ||
			trimmed.IndexOfAny(
				Path.GetInvalidFileNameChars()
			) >=
				0 ||
			trimmed.Contains(
				Path.DirectorySeparatorChar
			) ||
			trimmed.Contains(
				Path.AltDirectorySeparatorChar
			)
		)
		{
			throw new ArgumentException(
				"Unsafe directory name.",
				parameterName
			);
		}

		return trimmed;
	}

	private static string Sanitize(
		string value
	)
	{
		var invalid =
			Path.GetInvalidFileNameChars();

		var cleaned =
			new string(
				value.Select(
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
			cleaned
		)
			? "_"
			: cleaned;
	}
}

