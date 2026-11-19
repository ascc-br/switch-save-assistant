using System.Text;
using System.Text.Json;
using SwitchSaveAssistant.Core.Emulators;

namespace SwitchSaveAssistant.Tests;

public sealed class EdenMetadataServiceTests
{
	[Fact]
	public void ProfilesDat_MapeiaUuidInvertidoParaNome()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenMetadataProfiles-"
			)
			.FullName;

		try
		{
			var path =
				Path.Combine(
					root,
					"system",
					"save",
					"8000000000000010",
					"su",
					"avators",
					"profiles.dat"
				);

			Directory.CreateDirectory(
				Path.GetDirectoryName(
					path
				)!
			);

			var data =
				new byte[
					0x650
				];

			var userOffset =
				0x10;

			var uuid =
				Enumerable
					.Range(
						1,
						16
					)
					.Select(
						value =>
							(byte)value
					)
					.ToArray();

			uuid.CopyTo(
				data,
				userOffset
			);

			uuid.CopyTo(
				data,
				userOffset +
					0x10
			);

			Encoding.UTF8
				.GetBytes(
					"Alfran"
				)
				.CopyTo(
					data,
					userOffset +
						0x28
				);

			File.WriteAllBytes(
				path,
				data
			);

			var service =
				new EdenMetadataService();

			var profiles =
				service.ReadProfileNames(
					root
				);

			var expected =
				Convert.ToHexString(
					uuid
						.Reverse()
						.ToArray()
				);

			Assert.Equal(
				"Alfran",
				profiles[
					expected
				]
			);
		}
		finally
		{
			Directory.Delete(
				root,
				recursive: true
			);
		}
	}

	[Fact]
	public void CacheJson_MapeiaTitleIdParaNome()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenMetadataGames-"
			)
			.FullName;

		try
		{
			var cache =
				Path.Combine(
					root,
					"cache",
					"game_list"
				);

			Directory.CreateDirectory(
				cache
			);

			var json =
				JsonSerializer.Serialize(
					new
					{
						entries =
							new[]
							{
								new
								{
									program_id =
										"0100152000022000",

									title =
										"Mario Kart 8 Deluxe"
								}
							}
					}
				);

			File.WriteAllText(
				Path.Combine(
					cache,
					"game_metadata_cache.json"
				),
				json
			);

			var service =
				new EdenMetadataService();

			var games =
				service.ReadGameMetadata(
					new[]
						{
							root
						},
					new[]
						{
							cache
						}
				);

			Assert.Equal(
				"Mario Kart 8 Deluxe",
				games[
					"0100152000022000"
				]
				.Title
			);
		}
		finally
		{
			Directory.Delete(
				root,
				recursive: true
			);
		}
	}

	[Fact]
	public void AppNameTxt_EhFallbackDeNome()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenMetadataAppName-"
			)
			.FullName;

		try
		{
			var cache =
				Path.Combine(
					root,
					"cache",
					"game_list"
				);

			Directory.CreateDirectory(
				cache
			);

			File.WriteAllText(
				Path.Combine(
					cache,
					"0100F7E00C70E000.appname.txt"
				),
				"Hogwarts Legacy"
			);

			var service =
				new EdenMetadataService();

			var games =
				service.ReadGameMetadata(
					new[]
						{
							root
						},
					new[]
						{
							cache
						}
				);

			Assert.Equal(
				"Hogwarts Legacy",
				games[
					"0100F7E00C70E000"
				]
				.Title
			);
		}
		finally
		{
			Directory.Delete(
				root,
				recursive: true
			);
		}
	}

	[Fact]
	public void GameDirectoryFilename_MapeiaTitleIdParaNome()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenMetadataGameDir-"
			)
			.FullName;

		try
		{
			var roms =
				Path.Combine(
					root,
					"roms"
				);

			var nested =
				Path.Combine(
					roms,
					"Mario"
				);

			Directory.CreateDirectory(
				nested
			);

			File.WriteAllBytes(
				Path.Combine(
					nested,
					"Mario Kart 8 Deluxe [0100152000022000][v0].nsp"
				),
				Array.Empty<byte>()
			);

			var config =
				Path.Combine(
					root,
					"config"
				);

			Directory.CreateDirectory(
				config
			);

			File.WriteAllText(
				Path.Combine(
					config,
					"qt-config.ini"
				),
				$"Paths\\gamedirs\\1\\path={roms.Replace('\\', '/')}\n" +
				"Paths\\gamedirs\\1\\deep_scan=true\n"
			);

			var service =
				new EdenMetadataService();

			var games =
				service.ReadGameMetadata(
					new[]
						{
							root
						},
					Array.Empty<string>()
				);

			Assert.Equal(
				"Mario Kart 8 Deluxe",
				games[
					"0100152000022000"
				]
				.Title
			);
		}
		finally
		{
			Directory.Delete(
				root,
				recursive: true
			);
		}
	}

	[Fact]
	public void RecentFiles_MapeiaTitleIdParaNome()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenMetadataRecent-"
			)
			.FullName;

		try
		{
			var config =
				Path.Combine(
					root,
					"config"
				);

			Directory.CreateDirectory(
				config
			);

			File.WriteAllText(
				Path.Combine(
					config,
					"qt-config.ini"
				),
				"Paths\\recentFiles=\"D:/roms/Untitled Goose Game[010082400BCC6000][US][v0].nsp\"\n"
			);

			var service =
				new EdenMetadataService();

			var games =
				service.ReadGameMetadata(
					new[]
						{
							root
						},
					Array.Empty<string>()
				);

			Assert.Equal(
				"Untitled Goose Game",
				games[
					"010082400BCC6000"
				]
				.Title
			);
		}
		finally
		{
			Directory.Delete(
				root,
				recursive: true
			);
		}
	}

	[Fact]
	public void LogLoading_MapeiaTitleIdParaNome()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenMetadataLog-"
			)
			.FullName;

		try
		{
			var log =
				Path.Combine(
					root,
					"log"
				);

			Directory.CreateDirectory(
				log
			);

			File.WriteAllText(
				Path.Combine(
					log,
					"eden_log.txt"
				),
				"[  1.23] Core <Info> core\\core.cpp:327:Load: Loading Mario Party Superstars (01006FE013472000) ...\n"
			);

			var service =
				new EdenMetadataService();

			var games =
				service.ReadGameMetadata(
					new[]
						{
							root
						},
					Array.Empty<string>()
				);

			Assert.Equal(
				"Mario Party Superstars",
				games[
					"01006FE013472000"
				]
				.Title
			);
		}
		finally
		{
			Directory.Delete(
				root,
				recursive: true
			);
		}
	}
}
