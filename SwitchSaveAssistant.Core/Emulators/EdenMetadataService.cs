using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SwitchSaveAssistant.Core.Emulators;

public sealed record EdenGameMetadata(
	string TitleId,
	string Title,
	string? IconPath
);

public sealed class EdenMetadataService
{
	private const int ProfileDataSize =
		0x650;

	private const int HeaderSize =
		0x10;

	private const int UserRawSize =
		0xC8;

	private const int UserCount =
		8;

	private const int UuidSize =
		0x10;

	private const int UsernameOffset =
		0x28;

	private const int UsernameSize =
		0x20;

	private const string ProfilesRelativePath =
		"system\\save\\8000000000000010\\su\\avators\\profiles.dat";

	private static readonly Regex
		FileTitleIdRegex =
			new(
				@"\[(?<id>[0-9A-Fa-f]{16})\]",
				RegexOptions.Compiled
			);

	private static readonly Regex
		RecentPathRegex =
			new(
				@"(?<path>[^,\r\n]*?\[(?<id>[0-9A-Fa-f]{16})\][^,\r\n]*)",
				RegexOptions.Compiled
			);

	private static readonly Regex
		LogLoadingRegex =
			new(
				@"Loading\s+(?<title>.+?)\s+\((?<id>[0-9A-Fa-f]{16})\)",
				RegexOptions.Compiled |
				RegexOptions.IgnoreCase
			);

	private static readonly Regex
		LogBootRegex =
			new(
				@"Booting game:\s*(?<id>[0-9A-Fa-f]{16})\s*\|\s*(?<title>.+?)(?:\s+\(64-bit\)|\s+\|\s+|$)",
				RegexOptions.Compiled |
				RegexOptions.IgnoreCase
			);

	private static readonly HashSet<string>
		GameExtensions =
			new(
				new[]
				{
					".nsp",
					".nsz",
					".xci",
					".xcz",
					".nca"
				},
				StringComparer.OrdinalIgnoreCase
			);

	public IReadOnlyDictionary<string, string>
		ReadProfileNames(
			string nandRoot
		)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(
			nandRoot
		);

		var result =
			new Dictionary<string, string>(
				StringComparer.OrdinalIgnoreCase
			);

		var profilesPath =
			Path.Combine(
				Path.GetFullPath(
					nandRoot
				),
				ProfilesRelativePath
			);

		if (!File.Exists(
				profilesPath
			))
		{
			return result;
		}

		var data =
			File.ReadAllBytes(
				profilesPath
			);

		if (data.Length <
			ProfileDataSize)
		{
			return result;
		}

		for (var index = 0;
			index < UserCount;
			index++)
		{
			var offset =
				HeaderSize +
				index *
				UserRawSize;

			var uuid =
				data
					.AsSpan(
						offset,
						UuidSize
					)
					.ToArray();

			if (uuid.All(
				value =>
					value ==
					0
			))
			{
				continue;
			}

			var usernameBytes =
				data.AsSpan(
					offset +
						UsernameOffset,
					UsernameSize
				);

			var zeroIndex =
				usernameBytes.IndexOf(
					(byte)0
				);

			if (zeroIndex >=
				0)
			{
				usernameBytes =
					usernameBytes[
						..zeroIndex
					];
			}

			var username =
				Encoding.UTF8
					.GetString(
						usernameBytes
					)
					.Trim();

			if (string.IsNullOrWhiteSpace(
				username
			))
			{
				username =
					$"Perfil {index + 1}";
			}

			var reversed =
				uuid
					.Reverse()
					.ToArray();

			result[
				Convert.ToHexString(
					reversed
				)
			] =
				username;

			result[
				Convert.ToHexString(
					uuid
				)
			] =
				username;
		}

		return result;
	}

	public IReadOnlyDictionary<string, EdenGameMetadata>
		ReadGameMetadata(
			IEnumerable<string> edenDataRoots,
			IEnumerable<string> cacheDirectories
		)
	{
		ArgumentNullException.ThrowIfNull(
			edenDataRoots
		);

		ArgumentNullException.ThrowIfNull(
			cacheDirectories
		);

		var result =
			new Dictionary<string, EdenGameMetadata>(
				StringComparer.OrdinalIgnoreCase
			);

		var roots =
			edenDataRoots
				.Where(
					path =>
						!string.IsNullOrWhiteSpace(
							path
						)
				)
				.Select(
					path =>
						Path.GetFullPath(
							path
						)
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		/*
		 * 1. Cache textual legado/alternativo.
		 */
		foreach (var cacheDirectory
			in cacheDirectories
				.Where(
					path =>
						!string.IsNullOrWhiteSpace(
							path
						)
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				))
		{
			if (!Directory.Exists(
				cacheDirectory
			))
			{
				continue;
			}

			ReadMetadataJson(
				cacheDirectory,
				result
			);

			ReadAppNameFiles(
				cacheDirectory,
				result
			);

			AttachIcons(
				cacheDirectory,
				result
			);
		}

		/*
		 * 2. Configuração do Eden:
		 *    - Paths\recentFiles
		 *    - Paths\gamedirs\N\path
		 *    - deep_scan
		 */
		foreach (var root
			in roots)
		{
			ReadQtConfiguration(
				root,
				result
			);
		}

		/*
		 * 3. Logs: nomes dos títulos já carregados.
		 */
		foreach (var root
			in roots)
		{
			ReadLogs(
				root,
				result
			);
		}

		/*
		 * 4. Ícones: tenta anexar depois de todas as fontes.
		 */
		foreach (var cacheDirectory
			in cacheDirectories
				.Where(
					Directory.Exists
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				))
		{
			AttachIcons(
				cacheDirectory,
				result
			);
		}

		return result;
	}

	private static void ReadQtConfiguration(
		string edenRoot,
		Dictionary<string, EdenGameMetadata> result
	)
	{
		var path =
			Path.Combine(
				edenRoot,
				"config",
				"qt-config.ini"
			);

		if (!File.Exists(
				path
			))
		{
			return;
		}

		string[] lines;

		try
		{
			lines =
				File.ReadAllLines(
					path,
					Encoding.UTF8
				);
		}
		catch
		{
			return;
		}

		var gameDirectories =
			new Dictionary<int, string>();

		var deepScan =
			new Dictionary<int, bool>();

		foreach (var line
			in lines)
		{
			if (line.StartsWith(
				"Paths\\recentFiles=",
				StringComparison.OrdinalIgnoreCase
			))
			{
				ReadRecentFilesLine(
					line,
					result
				);

				continue;
			}

			var directoryMatch =
				Regex.Match(
					line,
					@"^Paths\\gamedirs\\(?<index>\d+)\\path=(?<path>.*)$",
					RegexOptions.IgnoreCase
				);

			if (directoryMatch.Success)
			{
				var index =
					int.Parse(
						directoryMatch.Groups[
							"index"
						]
						.Value
					);

				var configuredPath =
					directoryMatch.Groups[
						"path"
					]
					.Value
					.Trim();

				if (!string.IsNullOrWhiteSpace(
					configuredPath
				))
				{
					gameDirectories[index] =
						configuredPath;
				}

				continue;
			}

			var deepMatch =
				Regex.Match(
					line,
					@"^Paths\\gamedirs\\(?<index>\d+)\\deep_scan=(?<value>true|false)$",
					RegexOptions.IgnoreCase
				);

			if (deepMatch.Success)
			{
				deepScan[
					int.Parse(
						deepMatch.Groups[
							"index"
						]
						.Value
					)
				] =
					bool.Parse(
						deepMatch.Groups[
							"value"
						]
						.Value
					);
			}
		}

		foreach (var pair
			in gameDirectories)
		{
			if (IsVirtualGameDirectory(
				pair.Value
			))
			{
				continue;
			}

			var directory =
				Environment
					.ExpandEnvironmentVariables(
						pair.Value
					)
					.Replace(
						'/',
						Path.DirectorySeparatorChar
					);

			if (!Path.IsPathRooted(
				directory
			))
			{
				continue;
			}

			var recursive =
				deepScan.TryGetValue(
					pair.Key,
					out var configuredDeepScan
				) &&
				configuredDeepScan;

			ScanGameDirectory(
				directory,
				recursive,
				result
			);
		}
	}

	private static void ReadRecentFilesLine(
		string line,
		Dictionary<string, EdenGameMetadata> result
	)
	{
		foreach (Match match
			in RecentPathRegex.Matches(
				line
			))
		{
			var titleId =
				NormalizeTitleId(
					match.Groups[
						"id"
					]
					.Value
				);

			if (titleId is null)
			{
				continue;
			}

			var title =
				InferTitleFromPathText(
					match.Groups[
						"path"
					]
					.Value,
					titleId
				);

			AddMetadata(
				result,
				titleId,
				title,
				null
			);
		}
	}

	private static void ScanGameDirectory(
		string directory,
		bool recursive,
		Dictionary<string, EdenGameMetadata> result
	)
	{
		if (!Directory.Exists(
			directory
		))
		{
			return;
		}

		IEnumerable<string> files;

		try
		{
			files =
				Directory.EnumerateFiles(
					directory,
					"*",
					recursive
						? SearchOption.AllDirectories
						: SearchOption.TopDirectoryOnly
				);
		}
		catch
		{
			return;
		}

		foreach (var file
			in files)
		{
			if (!GameExtensions.Contains(
				Path.GetExtension(
					file
				)
			))
			{
				continue;
			}

			var fileName =
				Path.GetFileNameWithoutExtension(
					file
				);

			var match =
				FileTitleIdRegex.Match(
					fileName
				);

			if (!match.Success)
			{
				continue;
			}

			var titleId =
				NormalizeTitleId(
					match.Groups[
						"id"
					]
					.Value
				);

			if (titleId is null)
			{
				continue;
			}

			var title =
				fileName[
					..match.Index
				]
				.Trim(
					' ',
					'-',
					'_',
					'.'
				);

			/*
			 * Ex.: "Hogwarts Legacy [NSP] [0100...]"
			 */
			title =
				Regex.Replace(
					title,
					@"\s*\[(?:NSP|NSZ|XCI|XCZ)\]\s*$",
					string.Empty,
					RegexOptions.IgnoreCase
				)
				.Trim();

			if (string.IsNullOrWhiteSpace(
				title
			))
			{
				title =
					Path.GetFileName(
						Path.GetDirectoryName(
							file
						)
					);
			}

			AddMetadata(
				result,
				titleId,
				title,
				null
			);
		}
	}

	private static void ReadLogs(
		string edenRoot,
		Dictionary<string, EdenGameMetadata> result
	)
	{
		var logDirectory =
			Path.Combine(
				edenRoot,
				"log"
			);

		if (!Directory.Exists(
				logDirectory
			))
		{
			return;
		}

		IEnumerable<string> files;

		try
		{
			files =
				Directory.EnumerateFiles(
					logDirectory,
					"*",
					SearchOption.TopDirectoryOnly
				)
				.Where(
					path =>
						Path.GetFileName(
							path
						)
						.Contains(
							"log",
							StringComparison.OrdinalIgnoreCase
						)
				);
		}
		catch
		{
			return;
		}

		foreach (var file
			in files)
		{
			IEnumerable<string> lines;

			try
			{
				lines =
					File.ReadLines(
						file
					);
			}
			catch
			{
				continue;
			}

			foreach (var line
				in lines)
			{
				var loading =
					LogLoadingRegex.Match(
						line
					);

				if (loading.Success)
				{
					AddMetadata(
						result,
						loading.Groups[
							"id"
						]
						.Value,
						loading.Groups[
							"title"
						]
						.Value,
						null
					);
				}

				var boot =
					LogBootRegex.Match(
						line
					);

				if (boot.Success)
				{
					AddMetadata(
						result,
						boot.Groups[
							"id"
						]
						.Value,
						boot.Groups[
							"title"
						]
						.Value,
						null
					);
				}
			}
		}
	}

	private static string InferTitleFromPathText(
		string rawPath,
		string titleId
	)
	{
		var normalized =
			rawPath
				.Trim()
				.Trim(
					'"'
				)
				.Replace(
					'/',
					Path.DirectorySeparatorChar
				);

		var fileName =
			Path.GetFileNameWithoutExtension(
				normalized
			);

		var marker =
			$"[{titleId}]";

		var index =
			fileName.IndexOf(
				marker,
				StringComparison.OrdinalIgnoreCase
			);

		if (index >=
			0)
		{
			fileName =
				fileName[
					..index
				];
		}

		return fileName
			.Trim(
				' ',
				'-',
				'_',
				'.'
			);
	}

	private static bool IsVirtualGameDirectory(
		string value
	)
	{
		return value.Equals(
				"SDMC",
				StringComparison.OrdinalIgnoreCase
			) ||
			value.Equals(
				"UserNAND",
				StringComparison.OrdinalIgnoreCase
			) ||
			value.Equals(
				"SysNAND",
				StringComparison.OrdinalIgnoreCase
			);
	}

	private static void AddMetadata(
		Dictionary<string, EdenGameMetadata> result,
		string titleIdRaw,
		string? titleRaw,
		string? iconPath
	)
	{
		var titleId =
			NormalizeTitleId(
				titleIdRaw
			);

		var title =
			titleRaw
				?.Trim();

		if (titleId is null ||
			string.IsNullOrWhiteSpace(
				title
			))
		{
			return;
		}

		if (result.TryGetValue(
				titleId,
				out var existing
			))
		{
			if (existing.IconPath is null &&
				iconPath is not null)
			{
				result[titleId] =
					existing with
					{
						IconPath =
							iconPath
					};
			}

			return;
		}

		result[titleId] =
			new EdenGameMetadata(
				titleId,
				title,
				iconPath
			);
	}

	private static void ReadMetadataJson(
		string cacheDirectory,
		Dictionary<string, EdenGameMetadata> result
	)
	{
		var path =
			Path.Combine(
				cacheDirectory,
				"game_metadata_cache.json"
			);

		if (!File.Exists(
				path
			))
		{
			return;
		}

		try
		{
			using var document =
				JsonDocument.Parse(
					File.ReadAllText(
						path,
						Encoding.UTF8
					)
				);

			if (!document.RootElement.TryGetProperty(
				"entries",
				out var entries
			) ||
				entries.ValueKind !=
					JsonValueKind.Array)
			{
				return;
			}

			foreach (var entry
				in entries.EnumerateArray())
			{
				if (!entry.TryGetProperty(
						"program_id",
						out var idElement
					) ||
					!entry.TryGetProperty(
						"title",
						out var titleElement
					))
				{
					continue;
				}

				AddMetadata(
					result,
					idElement.GetString() ??
						string.Empty,
					titleElement.GetString(),
					null
				);
			}
		}
		catch
		{
			// Cache opcional.
		}
	}

	private static void ReadAppNameFiles(
		string cacheDirectory,
		Dictionary<string, EdenGameMetadata> result
	)
	{
		const string suffix =
			".appname.txt";

		IEnumerable<string> files;

		try
		{
			files =
				Directory.EnumerateFiles(
					cacheDirectory,
					"*.appname.txt",
					SearchOption.TopDirectoryOnly
				);
		}
		catch
		{
			return;
		}

		foreach (var path
			in files)
		{
			var fileName =
				Path.GetFileName(
					path
				);

			var rawId =
				fileName[
					..^suffix.Length
				];

			string title;

			try
			{
				title =
					File.ReadAllText(
						path,
						Encoding.UTF8
					)
					.Trim(
						'\0',
						'\r',
						'\n',
						' ',
						'\t'
					);
			}
			catch
			{
				continue;
			}

			AddMetadata(
				result,
				rawId,
				title,
				null
			);
		}
	}

	private static void AttachIcons(
		string cacheDirectory,
		Dictionary<string, EdenGameMetadata> result
	)
	{
		foreach (var pair
			in result.ToArray())
		{
			var candidates =
				new[]
					{
						Path.Combine(
							cacheDirectory,
							$"{pair.Key}.jpeg"
						),
						Path.Combine(
							cacheDirectory,
							$"{pair.Key}.jpg"
						),
						Path.Combine(
							cacheDirectory,
							$"{pair.Key}.png"
						)
					};

			var iconPath =
				candidates.FirstOrDefault(
					File.Exists
				);

			if (iconPath is null)
			{
				continue;
			}

			result[pair.Key] =
				pair.Value with
				{
					IconPath =
						iconPath
				};
		}
	}

	private static string? NormalizeTitleId(
		string? value
	)
	{
		if (string.IsNullOrWhiteSpace(
			value
		))
		{
			return null;
		}

		var normalized =
			value
				.Trim();

		if (normalized.StartsWith(
			"0x",
			StringComparison.OrdinalIgnoreCase
		))
		{
			normalized =
				normalized[2..];
		}

		if (normalized.Length >
				16 ||
			normalized.Length ==
				0 ||
			!normalized.All(
				Uri.IsHexDigit
			))
		{
			return null;
		}

		return normalized
			.PadLeft(
				16,
				'0'
			)
			.ToUpperInvariant();
	}
}
