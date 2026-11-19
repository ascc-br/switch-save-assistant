using SwitchSaveAssistant.Core.Emulators;

namespace SwitchSaveAssistant.Tests;

public sealed class EdenFolderRecencyServiceTests
{
	[Fact]
	public void EdenMaisNovo_EhDetectado()
	{
		var eden = Directory.CreateTempSubdirectory("EdenRecencyA-").FullName;
		var folder = Directory.CreateTempSubdirectory("EdenRecencyB-").FullName;

		try
		{
			var edenFile = Path.Combine(eden, "save.dat");
			var folderFile = Path.Combine(folder, "save.dat");

			File.WriteAllText(edenFile, "eden");
			File.WriteAllText(folderFile, "folder");

			var baseTime = new DateTime(
				2026, 11, 17, 20, 0, 0, DateTimeKind.Utc
			);

			File.SetLastWriteTimeUtc(folderFile, baseTime);
			File.SetLastWriteTimeUtc(edenFile, baseTime.AddMinutes(5));

			var result =
				new EdenFolderRecencyService()
					.Compare(eden, folder);

			Assert.Equal(
				EdenFolderRecencyState.EdenNewer,
				result.State
			);
		}
		finally
		{
			Directory.Delete(eden, true);
			Directory.Delete(folder, true);
		}
	}

	[Fact]
	public void DiferencaDeDoisSegundos_EhEmpate()
	{
		var eden = Directory.CreateTempSubdirectory("EdenRecencyA-").FullName;
		var folder = Directory.CreateTempSubdirectory("EdenRecencyB-").FullName;

		try
		{
			var edenFile = Path.Combine(eden, "save.dat");
			var folderFile = Path.Combine(folder, "save.dat");

			File.WriteAllText(edenFile, "eden");
			File.WriteAllText(folderFile, "folder");

			var baseTime = new DateTime(
				2026, 11, 17, 20, 0, 0, DateTimeKind.Utc
			);

			File.SetLastWriteTimeUtc(folderFile, baseTime);
			File.SetLastWriteTimeUtc(edenFile, baseTime.AddSeconds(2));

			var result =
				new EdenFolderRecencyService()
					.Compare(eden, folder);

			Assert.Equal(
				EdenFolderRecencyState.SameTime,
				result.State
			);
		}
		finally
		{
			Directory.Delete(eden, true);
			Directory.Delete(folder, true);
		}
	}

	[Fact]
	public void YuzuSaveSize_NaoInfluenciaDataMaisRecente()
	{
		var root = Directory.CreateTempSubdirectory(
			"EdenRecencyTechnical-"
		).FullName;

		try
		{
			var real = Path.Combine(root, "save.dat");
			var technical = Path.Combine(root, ".yuzu_save_size");

			File.WriteAllText(real, "save");
			File.WriteAllText(technical, "technical");

			var expected = new DateTime(
				2026, 11, 17, 20, 0, 0, DateTimeKind.Utc
			);

			File.SetLastWriteTimeUtc(real, expected);
			File.SetLastWriteTimeUtc(
				technical,
				expected.AddDays(1)
			);

			var latest =
				new EdenFolderRecencyService()
					.GetLatestComparableWriteTimeUtc(root);

			Assert.Equal(expected, latest);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}
}
