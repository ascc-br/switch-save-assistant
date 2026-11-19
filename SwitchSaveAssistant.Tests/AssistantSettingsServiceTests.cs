using SwitchSaveAssistant.Core.Settings;

namespace SwitchSaveAssistant.Tests;

public sealed class AssistantSettingsServiceTests
{
	[Fact]
	public void SaveLoad_PreservaConfiguracoes()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaSettings-"
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
						FolderPath =
							@"C:\SwitchSaveAssistant\TestData",

						EdenRootPath =
							@"C:\Eden",

						SwitchFilter =
							"Divergent",

						EdenFilter =
							"EdenOnly",

						EdenShowEmpty =
							true
					}
			);

			var loaded =
				service.Load();

			Assert.Equal(
				@"C:\SwitchSaveAssistant\TestData",
				loaded.FolderPath
			);

			Assert.Equal(
				@"C:\Eden",
				loaded.EdenRootPath
			);

			Assert.Equal(
				"Divergent",
				loaded.SwitchFilter
			);

			Assert.Equal(
				"EdenOnly",
				loaded.EdenFilter
			);

			Assert.True(
				loaded.EdenShowEmpty
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
	public void JsonInvalido_RetornaPadrao()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaSettingsInvalid-"
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
				"{ inválido"
			);

			var service =
				new AssistantSettingsService(
					path
				);

			var loaded =
				service.Load();

			Assert.Null(
				loaded.FolderPath
			);

			Assert.Equal(
				"All",
				loaded.SwitchFilter
			);

			Assert.Equal(
				"All",
				loaded.EdenFilter
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
	public void Update_PreservaCamposNaoAlterados()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaSettingsUpdate-"
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
						FolderPath =
							@"D:\Folder",

						EdenRootPath =
							@"C:\Eden",

						EdenFilter =
							"Divergent"
					}
			);

			service.Update(
				current =>
					current with
						{
							SwitchFilter =
								"SwitchOnly"
						}
			);

			var loaded =
				service.Load();

			Assert.Equal(
				@"D:\Folder",
				loaded.FolderPath
			);

			Assert.Equal(
				@"C:\Eden",
				loaded.EdenRootPath
			);

			Assert.Equal(
				"Divergent",
				loaded.EdenFilter
			);

			Assert.Equal(
				"SwitchOnly",
				loaded.SwitchFilter
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
