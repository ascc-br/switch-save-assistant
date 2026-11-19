using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Core.Emulators;

public sealed record EdenFolderDirectoryComparison(
	bool AreEqual,
	int ChangeCount,
	IReadOnlyList<string> SampleChanges,
	int EdenFileCount,
	int FolderFileCount
);

public sealed class EdenFolderComparisonService
{
	private readonly LocalSnapshotService
		_localSnapshotService =
			new();

	private readonly ManifestComparisonService
		_manifestComparisonService =
			new();

	public EdenFolderDirectoryComparison Compare(
		string edenPath,
		string folderPath
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(
			edenPath
		);

		ArgumentException.ThrowIfNullOrWhiteSpace(
			folderPath
		);

		if (!Directory.Exists(
				edenPath
			))
		{
			throw new DirectoryNotFoundException(
				$"Save do Eden não encontrado: {edenPath}"
			);
		}

		if (!Directory.Exists(
				folderPath
			))
		{
			throw new DirectoryNotFoundException(
				$"Save do Folder não encontrado: {folderPath}"
			);
		}

		var edenSnapshot =
			FilterTechnicalFiles(
				_localSnapshotService.Create(
					edenPath
				)
			);

		var folderSnapshot =
			FilterTechnicalFiles(
				_localSnapshotService.Create(
					folderPath
				)
			);

		var comparison =
			_manifestComparisonService.Compare(
				edenSnapshot,
				folderSnapshot
			);

		return new EdenFolderDirectoryComparison(
			comparison.AreEqual,
			comparison.Changes.Count,
			comparison.Changes
				.Take(
					3
				)
				.Select(
					change =>
						change.RelativePath
				)
				.ToArray(),
			edenSnapshot.Count,
			folderSnapshot.Count
		);
	}

	private static IReadOnlyDictionary<string, string>
		FilterTechnicalFiles(
			IReadOnlyDictionary<string, string> snapshot
		)
	{
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

	private static bool IsTechnicalFile(
		string relativePath
	)
	{
		var normalized =
			relativePath.Replace(
				'\\',
				'/'
			);

		var fileName =
			normalized
				.Split(
					'/',
					StringSplitOptions.RemoveEmptyEntries
				)
				.LastOrDefault()
			?? string.Empty;

		return string.Equals(
			fileName,
			".yuzu_save_size",
			StringComparison.OrdinalIgnoreCase
		);
	}
}
