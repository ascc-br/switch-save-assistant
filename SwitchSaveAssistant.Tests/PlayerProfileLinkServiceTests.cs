using SwitchSaveAssistant.Core.Profiles;

namespace SwitchSaveAssistant.Tests;

public sealed class PlayerProfileLinkServiceTests
{
	[Fact]
	public void Load_SemArquivo_RetornaDocumentoVazio()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaProfilesEmpty-"
			)
			.FullName;

		try
		{
			var service =
				new PlayerProfileLinkService(
					Path.Combine(
						root,
						"profiles.json"
					)
				);

			var document =
				service.Load();

			Assert.Equal(
				1,
				document.SchemaVersion
			);

			Assert.Empty(
				document.Profiles
			);
		}
		finally
		{
			Directory.Delete(
				root,
				true
			);
		}
	}

	[Fact]
	public void Bind_EdenStableId_Persiste()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaProfilesPersist-"
			)
			.FullName;

		try
		{
			var service =
				new PlayerProfileLinkService(
					Path.Combine(
						root,
						"profiles.json"
					)
				);

			var profile =
				service.CreateProfile(
					"Laurinha"
				);

			service.Bind(
				profile.Id,
				new PlayerProfileBinding
					{
						Platform =
							"Eden",

						NativeId =
							"241AA55C9FC9954AFAC2A0C24A473E47",

						DisplayName =
							"Laurinha",

						IdentityKind =
							ProfileIdentityKind.StableId
					}
			);

			var loaded =
				Assert.Single(
					service.Load()
						.Profiles
				);

			var binding =
				Assert.Single(
					loaded.Bindings
				);

			Assert.Equal(
				"Eden",
				binding.Platform
			);

			Assert.Equal(
				ProfileIdentityKind.StableId,
				binding.IdentityKind
			);
		}
		finally
		{
			Directory.Delete(
				root,
				true
			);
		}
	}

	[Fact]
	public void Bind_MesmoEndpointEmDoisJogadores_Bloqueia()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaProfilesConflict-"
			)
			.FullName;

		try
		{
			var service =
				new PlayerProfileLinkService(
					Path.Combine(
						root,
						"profiles.json"
					)
				);

			var first =
				service.CreateProfile(
					"Primeiro"
				);

			var second =
				service.CreateProfile(
					"Segundo"
				);

			var binding =
				new PlayerProfileBinding
					{
						Platform =
							"Eden",

						NativeId =
							"241AA55C9FC9954AFAC2A0C24A473E47",

						DisplayName =
							"Perfil",

						IdentityKind =
							ProfileIdentityKind.StableId
					};

			service.Bind(
				first.Id,
				binding
			);

			Assert.Throws<InvalidOperationException>(
				() =>
					service.Bind(
						second.Id,
						binding
					)
			);
		}
		finally
		{
			Directory.Delete(
				root,
				true
			);
		}
	}

	[Fact]
	public void Bind_DeviceEden_Bloqueia()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaProfilesDevice-"
			)
			.FullName;

		try
		{
			var service =
				new PlayerProfileLinkService(
					Path.Combine(
						root,
						"profiles.json"
					)
				);

			var profile =
				service.CreateProfile(
					"Jogador"
				);

			Assert.Throws<InvalidOperationException>(
				() =>
					service.Bind(
						profile.Id,
						new PlayerProfileBinding
							{
								Platform =
									"Eden",

								NativeId =
									"00000000000000000000000000000000",

								DisplayName =
									"Device",

								IdentityKind =
									ProfileIdentityKind.StableId
							}
					)
			);
		}
		finally
		{
			Directory.Delete(
				root,
				true
			);
		}
	}
}
