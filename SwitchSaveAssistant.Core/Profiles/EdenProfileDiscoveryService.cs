using SwitchSaveAssistant.Core.Emulators;

namespace SwitchSaveAssistant.Core.Profiles;

public sealed record EdenDiscoveredProfile(
	string NativeId,
	string? RegisteredName,
	bool IsRegistered,
	bool IsDevice,
	bool SaveDirectoryExists,
	int GameCount
);

public sealed record EdenProfileDiscoveryResult(
	string NandRoot,
	string SaveUsersRoot,
	IReadOnlyList<EdenDiscoveredProfile> Profiles
);

public sealed class EdenProfileDiscoveryService
{
	private const string AccountSaveSlot =
		"0000000000000000";

	private const string DeviceProfileId =
		"00000000000000000000000000000000";

	private readonly
		Func<string, IReadOnlyDictionary<string, string>>
		_readProfileNames;

	public EdenProfileDiscoveryService(
		Func<string, IReadOnlyDictionary<string, string>>?
			profileReader = null
	)
	{
		_readProfileNames =
			profileReader ??
			new EdenMetadataService()
				.ReadProfileNames;
	}

	public EdenProfileDiscoveryResult Discover(
		string edenRoot
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(
			edenRoot
		);

		var nandRoot =
			ResolveNandRoot(
				edenRoot
			) ??
			throw new DirectoryNotFoundException(
				"A valid Eden NAND root could not be found."
			);

		var saveUsersRoot =
			Path.Combine(
				nandRoot,
				"user",
				"save",
				AccountSaveSlot
			);

		var names =
			_readProfileNames(
				nandRoot
			);

		var ids =
			new HashSet<string>(
				names.Keys,
				StringComparer.OrdinalIgnoreCase
			);

		if (Directory.Exists(
			saveUsersRoot
		))
		{
			foreach (var directory
				in Directory.EnumerateDirectories(
					saveUsersRoot
				))
			{
				var id =
					Path.GetFileName(
						directory
					);

				if (IsProfileId(
					id
				))
				{
					ids.Add(
						id
					);
				}
			}
		}

		var profiles =
			ids
				.Select(
					id =>
						BuildProfile(
							id,
							names,
							saveUsersRoot
						)
				)
				.OrderByDescending(
					profile =>
						profile.IsDevice
				)
				.ThenByDescending(
					profile =>
						profile.IsRegistered
				)
				.ThenBy(
					profile =>
						profile.RegisteredName ??
						profile.NativeId,
					StringComparer.CurrentCultureIgnoreCase
				)
				.ToArray();

		return new EdenProfileDiscoveryResult(
			nandRoot,
			saveUsersRoot,
			profiles
		);
	}

	private static EdenDiscoveredProfile BuildProfile(
		string id,
		IReadOnlyDictionary<string, string> names,
		string saveUsersRoot
	)
	{
		var isDevice =
			string.Equals(
				id,
				DeviceProfileId,
				StringComparison.OrdinalIgnoreCase
			);

		var isRegistered =
			names.TryGetValue(
				id,
				out var registeredName
			);

		var saveDirectory =
			Path.Combine(
				saveUsersRoot,
				id
			);

		var exists =
			Directory.Exists(
				saveDirectory
			);

		var gameCount =
			exists
				? Directory.EnumerateDirectories(
						saveDirectory
					)
					.Count()
				: 0;

		return new EdenDiscoveredProfile(
			id,
			isRegistered
				? registeredName
				: null,
			isRegistered,
			isDevice,
			exists,
			gameCount
		);
	}

	private static string? ResolveNandRoot(
		string edenRoot
	)
	{
		var fullRoot =
			Path.GetFullPath(
				edenRoot
			);

		var candidates =
			new[]
				{
					Path.Combine(
						fullRoot,
						"nand"
					),

					fullRoot
				}
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				);

		foreach (var candidate
			in candidates)
		{
			var hasProfiles =
				File.Exists(
					Path.Combine(
						candidate,
						"system",
						"save",
						"8000000000000010",
						"su",
						"avators",
						"profiles.dat"
					)
				);

			var hasSaveRoot =
				Directory.Exists(
					Path.Combine(
						candidate,
						"user",
						"save",
						AccountSaveSlot
					)
				);

			if (hasProfiles ||
				hasSaveRoot)
			{
				return candidate;
			}
		}

		return null;
	}

	private static bool IsProfileId(
		string value
	)
	{
		return value.Length ==
				32 &&
			value.All(
				character =>
					char.IsAsciiHexDigit(
						character
					)
			);
	}
}
