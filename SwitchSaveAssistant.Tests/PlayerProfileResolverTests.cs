using SwitchSaveAssistant.Core.Profiles;

namespace SwitchSaveAssistant.Tests;

public sealed class PlayerProfileResolverTests
{
	private static LogicalPlayerProfile CreateSinnic()
	{
		return new LogicalPlayerProfile
			{
				Id =
					"logical-sinnic",

				DisplayName =
					"SiNNiC",

				Bindings =
					[
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
							},

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
							}
					]
			};
	}

	[Fact]
	public void ResolveFolderAlias_MapeiaNomeLogicoEAliasSwitch()
	{
		var resolver =
			new PlayerProfileResolver(
				new PlayerProfileLinksDocument
					{
						Profiles =
							[
								CreateSinnic()
							]
					}
			);

		var logical =
			resolver.ResolveFolderAlias(
				"SiNNiC"
			);

		var historical =
			resolver.ResolveFolderAlias(
				"Alfran"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Linked,
			logical.Kind
		);

		Assert.Equal(
			logical.LogicalPlayerId,
			historical.LogicalPlayerId
		);

		Assert.Equal(
			"SiNNiC",
			historical.LogicalPlayerName
		);
	}

	[Fact]
	public void ResolveSwitchName_UsaBindingDoSwitch()
	{
		var resolver =
			new PlayerProfileResolver(
				new PlayerProfileLinksDocument
					{
						Profiles =
							[
								CreateSinnic()
							]
					}
			);

		var result =
			resolver.ResolveSwitchName(
				"Alfran"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Linked,
			result.Kind
		);

		Assert.Equal(
			"logical-sinnic",
			result.LogicalPlayerId
		);
	}

	[Fact]
	public void ResolveSwitchName_NaoUsaNomeLogicoSemBindingCorrespondente()
	{
		var resolver =
			new PlayerProfileResolver(
				new PlayerProfileLinksDocument
					{
						Profiles =
							[
								CreateSinnic()
							]
					}
			);

		var result =
			resolver.ResolveSwitchName(
				"SiNNiC"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Unlinked,
			result.Kind
		);
	}

	[Fact]
	public void ResolveFolderAlias_NaoUsaIdEstavelComoNome()
	{
		var resolver =
			new PlayerProfileResolver(
				new PlayerProfileLinksDocument
					{
						Profiles =
							[
								CreateSinnic()
							]
					}
			);

		var result =
			resolver.ResolveFolderAlias(
				"BB3501CF6626D4D21E3246FA2C14ADA8"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Unlinked,
			result.Kind
		);
	}

	[Fact]
	public void ResolveFolderAlias_DetectaAliasAmbiguo()
	{
		var second =
			new LogicalPlayerProfile
				{
					Id =
						"logical-second",

					DisplayName =
						"Outro",

					Bindings =
						[
							new PlayerProfileBinding
								{
									Platform =
										"Switch",

									NativeId =
										"Outro",

									DisplayName =
										"Alfran",

									IdentityKind =
										ProfileIdentityKind.DisplayNameOnly
								}
						]
				};

		var resolver =
			new PlayerProfileResolver(
				new PlayerProfileLinksDocument
					{
						Profiles =
							[
								CreateSinnic(),
								second
							]
					}
			);

		var result =
			resolver.ResolveFolderAlias(
				"Alfran"
			);

		Assert.Equal(
			ProfileAliasResolutionKind.Ambiguous,
			result.Kind
		);
	}
}
