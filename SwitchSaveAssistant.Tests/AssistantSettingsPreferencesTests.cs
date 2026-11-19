using SwitchSaveAssistant.Core.Settings;

namespace SwitchSaveAssistant.Tests;

public sealed class AssistantSettingsPreferencesTests
{
	[Fact]
	public void Default_UsaInglesSistemaEAutomatico()
	{
		var settings =
			AssistantSettings.Default;

		Assert.Equal(
			"en-US",
			settings.Language
		);

		Assert.Equal(
			"System",
			settings.Theme
		);

		Assert.Equal(
			"Automatic",
			settings.DefaultEmulator
		);
	}

	[Fact]
	public void Load_JsonLegadoPreservaDadosEAdicionaNovosDefaults()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaSettingsLegacy-"
			)
			.FullName;

		try
		{
			var path =
				Path.Combine(
					root,
					"settings.json"
				);

			File.WriteAllText(
				path,
				"""
				{
				  "FolderPath": "C:\\SwitchSaveAssistant\\TestData",
				  "EdenRootPath": "C:\\eden",
				  "SwitchFilter": "Divergent",
				  "EdenFilter": "All",
				  "EdenShowEmpty": true
				}
				"""
			);

			var settings =
				new AssistantSettingsService(
					path
				)
				.Load();

			Assert.Equal(
				@"C:\SwitchSaveAssistant\TestData",
				settings.FolderPath
			);

			Assert.Equal(
				@"C:\eden",
				settings.EdenRootPath
			);

			Assert.Equal(
				"Divergent",
				settings.SwitchFilter
			);

			Assert.True(
				settings.EdenShowEmpty
			);

			Assert.Equal(
				"en-US",
				settings.Language
			);

			Assert.Equal(
				"System",
				settings.Theme
			);

			Assert.Equal(
				"Automatic",
				settings.DefaultEmulator
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
	public void Save_NormalizaValoresInvalidos()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaSettingsNormalize-"
			)
			.FullName;

		try
		{
			var path =
				Path.Combine(
					root,
					"settings.json"
				);

			var service =
				new AssistantSettingsService(
					path
				);

			service.Save(
				new AssistantSettings
					{
						Language =
							"xx-XX",

						Theme =
							"Blue",

						DefaultEmulator =
							"Unknown"
					}
			);

			var settings =
				service.Load();

			Assert.Equal(
				"en-US",
				settings.Language
			);

			Assert.Equal(
				"System",
				settings.Theme
			);

			Assert.Equal(
				"Automatic",
				settings.DefaultEmulator
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
