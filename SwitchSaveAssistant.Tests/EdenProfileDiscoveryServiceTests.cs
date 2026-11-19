using SwitchSaveAssistant.Core.Profiles;

namespace SwitchSaveAssistant.Tests;

public sealed class EdenProfileDiscoveryServiceTests
{
	[Fact]
	public void Discover_UneRegistradosOrfaosEDevice()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaEdenProfiles-"
			)
			.FullName;

		try
		{
			var nand =
				Path.Combine(
					root,
					"nand"
				);

			var saveRoot =
				Path.Combine(
					nand,
					"user",
					"save",
					"0000000000000000"
				);

			Directory.CreateDirectory(
				saveRoot
			);

			var registeredId =
				"11111111111111111111111111111111";

			var orphanId =
				"22222222222222222222222222222222";

			var deviceId =
				"00000000000000000000000000000000";

			Directory.CreateDirectory(
				Path.Combine(
					saveRoot,
					registeredId,
					"0100000000000001"
				)
			);

			Directory.CreateDirectory(
				Path.Combine(
					saveRoot,
					orphanId,
					"0100000000000002"
				)
			);

			Directory.CreateDirectory(
				Path.Combine(
					saveRoot,
					deviceId,
					"0100000000000003"
				)
			);

			var service =
				new EdenProfileDiscoveryService(
					_ =>
						new Dictionary<string, string>(
							StringComparer.OrdinalIgnoreCase
						)
							{
								[registeredId] =
									"Laurinha"
							}
				);

			var result =
				service.Discover(
					root
				);

			Assert.Equal(
				3,
				result.Profiles.Count
			);

			var registered =
				Assert.Single(
					result.Profiles,
					item =>
						item.NativeId ==
						registeredId
				);

			Assert.True(
				registered.IsRegistered
			);

			Assert.Equal(
				"Laurinha",
				registered.RegisteredName
			);

			Assert.Equal(
				1,
				registered.GameCount
			);

			var orphan =
				Assert.Single(
					result.Profiles,
					item =>
						item.NativeId ==
						orphanId
				);

			Assert.False(
				orphan.IsRegistered
			);

			Assert.False(
				orphan.IsDevice
			);

			var device =
				Assert.Single(
					result.Profiles,
					item =>
						item.NativeId ==
						deviceId
				);

			Assert.True(
				device.IsDevice
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
	public void Discover_PerfilRegistradoSemSave_TambemAparece()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaEdenRegistered-"
			)
			.FullName;

		try
		{
			var nand =
				Path.Combine(
					root,
					"nand"
				);

			Directory.CreateDirectory(
				Path.Combine(
					nand,
					"user",
					"save",
					"0000000000000000"
				)
			);

			var id =
				"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

			var service =
				new EdenProfileDiscoveryService(
					_ =>
						new Dictionary<string, string>
							{
								[id] =
									"SemSave"
							}
				);

			var profile =
				Assert.Single(
					service.Discover(
						root
					)
					.Profiles
				);

			Assert.True(
				profile.IsRegistered
			);

			Assert.False(
				profile.SaveDirectoryExists
			);

			Assert.Equal(
				0,
				profile.GameCount
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



