using SwitchSaveAssistant.Core.Profiles;

namespace SwitchSaveAssistant.Tests;

public sealed class PlayerProfileResolverEdenTests
{
	private static PlayerProfileLinksDocument CreateDocument()
	{
		return new PlayerProfileLinksDocument
			{
				Profiles =
					[
						new LogicalPlayerProfile
							{
								Id =
									"logical-alf-sinnic",

								DisplayName =
									"Alf-Sinnic",

								Bindings =
									[
										new PlayerProfileBinding
											{
												Platform =
													"Eden",

												NativeId =
													"BB3501CF6626D4D21E3246FA2C14ADA8",

												DisplayName =
													"SiNNiC",

												IdentityKind =
													ProfileIdentityKind.StableId
											},

										new PlayerProfileBinding
											{
												Platform =
													"Switch",

												NativeId =
													"Alfran",

												DisplayName =
													"Alfran",

												IdentityKind =
													ProfileIdentityKind.DisplayNameOnly
											}
									]
							}
					]
			};
	}

	[Fact]
	public void ResolveEdenProfileId_MapeiaIdEstavel()
	{
		var resolver =
			new PlayerProfileResolver(
				CreateDocument()
			);

		var result =
			resolver.ResolveEdenProfileId(
				"BB3501CF6626D4D21E3246FA2C14ADA8"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Linked,
			result.Kind
		);

		Assert.Equal(
			"logical-alf-sinnic",
			result.LogicalPlayerId
		);

		Assert.Equal(
			"Alf-Sinnic",
			result.LogicalPlayerName
		);
	}

	[Fact]
	public void ResolveEdenProfileId_EhCaseInsensitive()
	{
		var resolver =
			new PlayerProfileResolver(
				CreateDocument()
			);

		var result =
			resolver.ResolveEdenProfileId(
				"bb3501cf6626d4d21e3246fa2c14ada8"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Linked,
			result.Kind
		);
	}

	[Fact]
	public void ResolveEdenProfileId_NaoUsaNomeVisivelComoId()
	{
		var resolver =
			new PlayerProfileResolver(
				CreateDocument()
			);

		var result =
			resolver.ResolveEdenProfileId(
				"SiNNiC"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Unlinked,
			result.Kind
		);
	}

	[Fact]
	public void ResolveProviderNativeId_RespeitaPlataforma()
	{
		var resolver =
			new PlayerProfileResolver(
				CreateDocument()
			);

		var eden =
			resolver.ResolveProviderNativeId(
				"Eden",
				"Alfran"
			);

		var switchResult =
			resolver.ResolveProviderNativeId(
				"Switch",
				"Alfran"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Unlinked,
			eden.Kind
		);

		Assert.Equal(
			ProfileAliasResolutionKind.Linked,
			switchResult.Kind
		);
	}
}
