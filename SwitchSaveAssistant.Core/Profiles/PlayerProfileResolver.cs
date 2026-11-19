namespace SwitchSaveAssistant.Core.Profiles;

public enum ProfileAliasResolutionKind
{
	Unlinked,
	Linked,
	Ambiguous
}

public sealed record ProfileAliasResolution(
	ProfileAliasResolutionKind Kind,
	string NativeName,
	string? LogicalPlayerId = null,
	string? LogicalPlayerName = null
);

public sealed class PlayerProfileResolver
{
	private readonly
		PlayerProfileLinksDocument
		_document;

	public PlayerProfileResolver(
		PlayerProfileLinksDocument document
	)
	{
		_document =
			document ??
			throw new ArgumentNullException(
				nameof(
					document
				)
			);
	}

	public ProfileAliasResolution ResolveFolderAlias(
		string nativeName
	)
	{
		return Resolve(
			nativeName,
			GetFolderAliases
		);
	}

	public ProfileAliasResolution ResolveSwitchName(
		string nativeName
	)
	{
		return Resolve(
			nativeName,
			GetSwitchAliases
		);
	}

	public ProfileAliasResolution ResolveEdenProfileId(
		string nativeId
	)
	{
		return ResolveProviderNativeId(
			"Eden",
			nativeId
		);
	}

	public ProfileAliasResolution ResolveProviderNativeId(
		string platform,
		string nativeId
	)
	{
		if (string.IsNullOrWhiteSpace(
			platform
		))
		{
			return new ProfileAliasResolution(
				ProfileAliasResolutionKind.Unlinked,
				nativeId ??
					string.Empty
			);
		}

		return Resolve(
			nativeId,
			profile =>
				GetProviderNativeIds(
					profile,
					platform
				)
		);
	}

	private ProfileAliasResolution Resolve(
		string nativeName,
		Func<LogicalPlayerProfile, IEnumerable<string>>
			aliasSelector
	)
	{
		if (string.IsNullOrWhiteSpace(
			nativeName
		))
		{
			return new ProfileAliasResolution(
				ProfileAliasResolutionKind.Unlinked,
				nativeName ??
					string.Empty
			);
		}

		var normalized =
			nativeName.Trim();

		var matches =
			_document.Profiles
				.Where(
					profile =>
						aliasSelector(
							profile
						)
						.Any(
							alias =>
								string.Equals(
									alias,
									normalized,
									StringComparison.OrdinalIgnoreCase
								)
						)
				)
				.GroupBy(
					profile =>
						profile.Id,
					StringComparer.OrdinalIgnoreCase
				)
				.Select(
					group =>
						group.First()
				)
				.ToArray();

		if (matches.Length ==
			0)
		{
			return new ProfileAliasResolution(
				ProfileAliasResolutionKind.Unlinked,
				normalized
			);
		}

		if (matches.Length >
			1)
		{
			return new ProfileAliasResolution(
				ProfileAliasResolutionKind.Ambiguous,
				normalized
			);
		}

		return new ProfileAliasResolution(
			ProfileAliasResolutionKind.Linked,
			normalized,
			matches[0].Id,
			matches[0].DisplayName
		);
	}

	private static IEnumerable<string>
		GetFolderAliases(
			LogicalPlayerProfile profile
		)
	{
		yield return profile.DisplayName;

		foreach (var binding
			in profile.Bindings)
		{
			if (!string.IsNullOrWhiteSpace(
				binding.DisplayName
			))
			{
				yield return binding.DisplayName;
			}

			if (
				binding.IdentityKind ==
					ProfileIdentityKind.DisplayNameOnly &&
				!string.IsNullOrWhiteSpace(
					binding.NativeId
				)
			)
			{
				yield return binding.NativeId;
			}
		}
	}

	private static IEnumerable<string>
		GetSwitchAliases(
			LogicalPlayerProfile profile
		)
	{
		foreach (var binding
			in profile.Bindings.Where(
				binding =>
					string.Equals(
						binding.Platform,
						"Switch",
						StringComparison.OrdinalIgnoreCase
					)
			))
		{
			if (!string.IsNullOrWhiteSpace(
				binding.NativeId
			))
			{
				yield return binding.NativeId;
			}

			if (!string.IsNullOrWhiteSpace(
				binding.DisplayName
			))
			{
				yield return binding.DisplayName;
			}
		}
	}

	private static IEnumerable<string>
		GetProviderNativeIds(
			LogicalPlayerProfile profile,
			string platform
		)
	{
		foreach (var binding
			in profile.Bindings.Where(
				binding =>
					string.Equals(
						binding.Platform,
						platform,
						StringComparison.OrdinalIgnoreCase
					) &&
					!string.IsNullOrWhiteSpace(
						binding.NativeId
					)
			))
		{
			yield return binding.NativeId;
		}
	}
}
