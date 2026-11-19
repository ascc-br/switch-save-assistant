using SwitchSaveAssistant.Core.Emulators;

namespace SwitchSaveAssistant.Tests;

public sealed class EdenFolderComparisonServiceTests
{
	[Fact]
	public void ConteudoIgual_EhSincronizado()
	{
		var eden =
			Directory.CreateTempSubdirectory(
				"EdenCompareA-"
			)
			.FullName;

		var folder =
			Directory.CreateTempSubdirectory(
				"EdenCompareB-"
			)
			.FullName;

		try
		{
			Directory.CreateDirectory(
				Path.Combine(
					eden,
					"sub"
				)
			);

			Directory.CreateDirectory(
				Path.Combine(
					folder,
					"sub"
				)
			);

			File.WriteAllText(
				Path.Combine(
					eden,
					"sub",
					"save.dat"
				),
				"mesmo conteúdo"
			);

			File.WriteAllText(
				Path.Combine(
					folder,
					"sub",
					"save.dat"
				),
				"mesmo conteúdo"
			);

			var service =
				new EdenFolderComparisonService();

			var result =
				service.Compare(
					eden,
					folder
				);

			Assert.True(
				result.AreEqual
			);

			Assert.Equal(
				0,
				result.ChangeCount
			);
		}
		finally
		{
			Directory.Delete(
				eden,
				recursive: true
			);

			Directory.Delete(
				folder,
				recursive: true
			);
		}
	}

	[Fact]
	public void ConteudoDiferente_EhDivergente()
	{
		var eden =
			Directory.CreateTempSubdirectory(
				"EdenCompareA-"
			)
			.FullName;

		var folder =
			Directory.CreateTempSubdirectory(
				"EdenCompareB-"
			)
			.FullName;

		try
		{
			File.WriteAllText(
				Path.Combine(
					eden,
					"save.dat"
				),
				"eden"
			);

			File.WriteAllText(
				Path.Combine(
					folder,
					"save.dat"
				),
				"folder"
			);

			var service =
				new EdenFolderComparisonService();

			var result =
				service.Compare(
					eden,
					folder
				);

			Assert.False(
				result.AreEqual
			);

			Assert.Equal(
				1,
				result.ChangeCount
			);
		}
		finally
		{
			Directory.Delete(
				eden,
				recursive: true
			);

			Directory.Delete(
				folder,
				recursive: true
			);
		}
	}

	[Fact]
	public void YuzuSaveSize_EhIgnorado()
	{
		var eden =
			Directory.CreateTempSubdirectory(
				"EdenCompareA-"
			)
			.FullName;

		var folder =
			Directory.CreateTempSubdirectory(
				"EdenCompareB-"
			)
			.FullName;

		try
		{
			File.WriteAllText(
				Path.Combine(
					eden,
					"save.dat"
				),
				"igual"
			);

			File.WriteAllText(
				Path.Combine(
					folder,
					"save.dat"
				),
				"igual"
			);

			File.WriteAllText(
				Path.Combine(
					eden,
					".yuzu_save_size"
				),
				"arquivo técnico"
			);

			var service =
				new EdenFolderComparisonService();

			var result =
				service.Compare(
					eden,
					folder
				);

			Assert.True(
				result.AreEqual
			);

			Assert.Equal(
				1,
				result.EdenFileCount
			);

			Assert.Equal(
				1,
				result.FolderFileCount
			);
		}
		finally
		{
			Directory.Delete(
				eden,
				recursive: true
			);

			Directory.Delete(
				folder,
				recursive: true
			);
		}
	}
}
