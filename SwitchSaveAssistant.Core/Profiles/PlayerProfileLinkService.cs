using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwitchSaveAssistant.Core.Profiles;

public enum ProfileIdentityKind
{
	StableId,
	DisplayNameOnly
}

public sealed record PlayerProfileBinding
{
	public string Platform
	{
		get;
		init;
	} =
		string.Empty;

	public string NativeId
	{
		get;
		init;
	} =
		string.Empty;

	public string DisplayName
	{
		get;
		init;
	} =
		string.Empty;

	[JsonConverter(
		typeof(
			JsonStringEnumConverter
		)
	)]
	public ProfileIdentityKind IdentityKind
	{
		get;
		init;
	}
}

public sealed record LogicalPlayerProfile
{
	public string Id
	{
		get;
		init;
	} =
		string.Empty;

	public string DisplayName
	{
		get;
		init;
	} =
		string.Empty;

	public List<PlayerProfileBinding> Bindings
	{
		get;
		init;
	} =
		new();
}

public sealed record PlayerProfileLinksDocument
{
	public int SchemaVersion
	{
		get;
		init;
	} =
		1;

	public List<LogicalPlayerProfile> Profiles
	{
		get;
		init;
	} =
		new();
}

public sealed class PlayerProfileLinkService
{
	private const string
		DeviceProfileId =
			"00000000000000000000000000000000";

	private static readonly
		HashSet<string>
		AllowedPlatforms =
			new(
				new[]
					{
						"Eden",
						"Switch",
						"Citron",
						"Ryujinx"
					},
				StringComparer.OrdinalIgnoreCase
			);

	private static readonly
		JsonSerializerOptions
		JsonOptions =
			new()
				{
					WriteIndented =
						true,

					PropertyNameCaseInsensitive =
						true
				};

	public string ProfilesPath
	{
		get;
	}

	public PlayerProfileLinkService(
		string? profilesPath = null
	)
	{
		ProfilesPath =
			profilesPath ??
			Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData
				),
				"SwitchSaveAssistant",
				"profiles.json"
			);
	}

	public PlayerProfileLinksDocument Load()
	{
		try
		{
			if (!File.Exists(
				ProfilesPath
			))
			{
				return new PlayerProfileLinksDocument();
			}

			var document =
				JsonSerializer.Deserialize<PlayerProfileLinksDocument>(
					File.ReadAllText(
						ProfilesPath
					),
					JsonOptions
				) ??
				new PlayerProfileLinksDocument();

			return Normalize(
				document
			);
		}
		catch
		{
			return new PlayerProfileLinksDocument();
		}
	}

	public void Save(
		PlayerProfileLinksDocument document
	)
	{
		ArgumentNullException.ThrowIfNull(
			document
		);

		var normalized =
			Normalize(
				document
			);

		ValidateDocument(
			normalized
		);

		var directory =
			Path.GetDirectoryName(
				ProfilesPath
			);

		if (string.IsNullOrWhiteSpace(
			directory
		))
		{
			throw new InvalidOperationException(
				"Could not determine the player profile directory."
			);
		}

		Directory.CreateDirectory(
			directory
		);

		var temporaryPath =
			ProfilesPath +
			".tmp";

		File.WriteAllText(
			temporaryPath,
			JsonSerializer.Serialize(
				normalized,
				JsonOptions
			)
		);

		File.Move(
			temporaryPath,
			ProfilesPath,
			overwrite: true
		);
	}

	public LogicalPlayerProfile CreateProfile(
		string displayName
	)
	{
		var normalizedName =
			RequireText(
				displayName,
				nameof(
					displayName
				)
			);

		var document =
			Load();

		if (document.Profiles.Any(
				profile =>
					string.Equals(
						profile.DisplayName,
						normalizedName,
						StringComparison.OrdinalIgnoreCase
					)
			))
		{
			throw new InvalidOperationException(
				"A logical player with this name already exists."
			);
		}

		var profile =
			new LogicalPlayerProfile
				{
					Id =
						Guid.NewGuid()
							.ToString(
								"N"
							),

					DisplayName =
						normalizedName
				};

		document.Profiles.Add(
			profile
		);

		Save(
			document
		);

		return profile;
	}

	public void DeleteProfile(
		string profileId
	)
	{
		var document =
			Load();

		var removed =
			document.Profiles.RemoveAll(
				profile =>
					string.Equals(
						profile.Id,
						profileId,
						StringComparison.OrdinalIgnoreCase
					)
			);

		if (removed ==
			0)
		{
			throw new InvalidOperationException(
				"Logical player profile not found."
			);
		}

		Save(
			document
		);
	}

	public void Bind(
		string profileId,
		PlayerProfileBinding binding
	)
	{
		ArgumentNullException.ThrowIfNull(
			binding
		);

		var normalizedBinding =
			NormalizeBinding(
				binding
			);

		ValidateBinding(
			normalizedBinding
		);

		var document =
			Load();

		var profile =
			document.Profiles.FirstOrDefault(
				item =>
					string.Equals(
						item.Id,
						profileId,
						StringComparison.OrdinalIgnoreCase
					)
			) ??
			throw new InvalidOperationException(
				"Logical player profile not found."
			);

		var conflict =
			document.Profiles
				.Where(
					item =>
						!string.Equals(
							item.Id,
							profile.Id,
							StringComparison.OrdinalIgnoreCase
						)
				)
				.SelectMany(
					item =>
						item.Bindings
				)
				.Any(
					existing =>
						SameEndpoint(
							existing,
							normalizedBinding
						)
				);

		if (conflict)
		{
			throw new InvalidOperationException(
				"This native profile is already linked to another logical player."
			);
		}

		profile.Bindings.RemoveAll(
			existing =>
				string.Equals(
					existing.Platform,
					normalizedBinding.Platform,
					StringComparison.OrdinalIgnoreCase
				)
		);

		profile.Bindings.Add(
			normalizedBinding
		);

		Save(
			document
		);
	}

	public void Unbind(
		string profileId,
		string platform
	)
	{
		var normalizedPlatform =
			RequireText(
				platform,
				nameof(
					platform
				)
			);

		var document =
			Load();

		var profile =
			document.Profiles.FirstOrDefault(
				item =>
					string.Equals(
						item.Id,
						profileId,
						StringComparison.OrdinalIgnoreCase
					)
			) ??
			throw new InvalidOperationException(
				"Logical player profile not found."
			);

		profile.Bindings.RemoveAll(
			existing =>
				string.Equals(
					existing.Platform,
					normalizedPlatform,
					StringComparison.OrdinalIgnoreCase
				)
		);

		Save(
			document
		);
	}

	private static PlayerProfileLinksDocument
		Normalize(
			PlayerProfileLinksDocument document
		)
	{
		var profiles =
			document.Profiles
				.Where(
					profile =>
						!string.IsNullOrWhiteSpace(
							profile.Id
						) &&
						!string.IsNullOrWhiteSpace(
							profile.DisplayName
						)
				)
				.Select(
					profile =>
						profile with
							{
								Id =
									profile.Id.Trim(),

								DisplayName =
									profile.DisplayName.Trim(),

								Bindings =
									profile.Bindings
										.Select(
											NormalizeBinding
										)
										.ToList()
							}
				)
				.ToList();

		return new PlayerProfileLinksDocument
			{
				SchemaVersion =
					1,

				Profiles =
					profiles
			};
	}

	private static PlayerProfileBinding
		NormalizeBinding(
			PlayerProfileBinding binding
		)
	{
		return binding with
			{
				Platform =
					binding.Platform.Trim(),

				NativeId =
					binding.NativeId.Trim(),

				DisplayName =
					binding.DisplayName.Trim()
			};
	}

	private static void ValidateDocument(
		PlayerProfileLinksDocument document
	)
	{
		var ids =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase
			);

		var names =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase
			);

		var endpoints =
			new HashSet<string>(
				StringComparer.OrdinalIgnoreCase
			);

		foreach (var profile
			in document.Profiles)
		{
			if (!ids.Add(
				profile.Id
			))
			{
				throw new InvalidDataException(
					"Duplicate logical player ID."
				);
			}

			if (!names.Add(
				profile.DisplayName
			))
			{
				throw new InvalidDataException(
					"Duplicate logical player name."
				);
			}

			var platforms =
				new HashSet<string>(
					StringComparer.OrdinalIgnoreCase
				);

			foreach (var binding
				in profile.Bindings)
			{
				ValidateBinding(
					binding
				);

				if (!platforms.Add(
					binding.Platform
				))
				{
					throw new InvalidDataException(
						"Each logical player can have only one binding per platform."
					);
				}

				if (!endpoints.Add(
					BuildEndpointKey(
						binding
					)
				))
				{
					throw new InvalidDataException(
						"The same native profile cannot be linked more than once."
					);
				}
			}
		}
	}

	private static void ValidateBinding(
		PlayerProfileBinding binding
	)
	{
		if (!AllowedPlatforms.Contains(
			binding.Platform
		))
		{
			throw new ArgumentException(
				"Unsupported profile platform.",
				nameof(
					binding
				)
			);
		}

		if (string.IsNullOrWhiteSpace(
			binding.NativeId
		))
		{
			throw new ArgumentException(
				"Native profile identity is required.",
				nameof(
					binding
				)
			);
		}

		if (
			string.Equals(
				binding.Platform,
				"Eden",
				StringComparison.OrdinalIgnoreCase
			) &&
			string.Equals(
				binding.NativeId,
				DeviceProfileId,
				StringComparison.OrdinalIgnoreCase
			)
		)
		{
			throw new InvalidOperationException(
				"Device/shared cannot be linked to a player."
			);
		}
	}

	private static bool SameEndpoint(
		PlayerProfileBinding left,
		PlayerProfileBinding right
	)
	{
		return string.Equals(
				left.Platform,
				right.Platform,
				StringComparison.OrdinalIgnoreCase
			) &&
			string.Equals(
				left.NativeId,
				right.NativeId,
				StringComparison.OrdinalIgnoreCase
			);
	}

	private static string BuildEndpointKey(
		PlayerProfileBinding binding
	)
	{
		return binding.Platform +
			"\u001f" +
			binding.NativeId;
	}

	private static string RequireText(
		string value,
		string parameterName
	)
	{
		if (string.IsNullOrWhiteSpace(
			value
		))
		{
			throw new ArgumentException(
				"Value cannot be empty.",
				parameterName
			);
		}

		return value.Trim();
	}
}
