namespace SwitchSaveAssistant.Core.Emulators;

public enum EdenFolderRecencyState
{
	Unknown,
	SameTime,
	EdenNewer,
	FolderNewer
}

public sealed record EdenFolderRecencyInfo(
	DateTime? EdenLatestUtc,
	DateTime? FolderLatestUtc,
	EdenFolderRecencyState State
);

public sealed class EdenFolderRecencyService
{
	private static readonly TimeSpan DefaultTolerance =
		TimeSpan.FromSeconds(2);

	public EdenFolderRecencyInfo Compare(
		string? edenPath,
		string? folderPath,
		TimeSpan? tolerance = null
	)
	{
		var eden = GetLatestComparableWriteTimeUtc(edenPath);
		var folder = GetLatestComparableWriteTimeUtc(folderPath);

		if (eden is null || folder is null)
		{
			return new EdenFolderRecencyInfo(
				eden,
				folder,
				EdenFolderRecencyState.Unknown
			);
		}

		var difference = eden.Value - folder.Value;

		if (difference.Duration() <= (tolerance ?? DefaultTolerance))
		{
			return new EdenFolderRecencyInfo(
				eden,
				folder,
				EdenFolderRecencyState.SameTime
			);
		}

		return new EdenFolderRecencyInfo(
			eden,
			folder,
			difference > TimeSpan.Zero
				? EdenFolderRecencyState.EdenNewer
				: EdenFolderRecencyState.FolderNewer
		);
	}

	public DateTime? GetLatestComparableWriteTimeUtc(string? path)
	{
		if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
		{
			return null;
		}

		DateTime? latest = null;

		try
		{
			foreach (var file in Directory.EnumerateFiles(
				path,
				"*",
				SearchOption.AllDirectories
			))
			{
				if (string.Equals(
					Path.GetFileName(file),
					".yuzu_save_size",
					StringComparison.OrdinalIgnoreCase
				))
				{
					continue;
				}

				DateTime writeTime;

				try
				{
					writeTime = File.GetLastWriteTimeUtc(file);
				}
				catch
				{
					continue;
				}

				if (latest is null || writeTime > latest.Value)
				{
					latest = writeTime;
				}
			}
		}
		catch
		{
			return null;
		}

		return latest;
	}
}
