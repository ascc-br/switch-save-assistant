using SwitchSaveAssistant.Core.Profiles;

namespace SwitchSaveAssistant.Tests;

public sealed class ProfileAliasRepairServiceTests
{
	[Fact]
	public void BuildPlan_IdenticalAliases_MarcaComoIdentico()
	{
		using var temp =
			new TemporaryDirectory();

		var gameRoot =
			CreateGame(
				temp.Path,
				"Game"
			);

		WriteSave(
			gameRoot,
			"Alfran",
			"save.dat",
			"same"
		);

		WriteSave(
			gameRoot,
			"SiNNiC",
			"save.dat",
			"same"
		);

		var service =
			new ProfileAliasRepairService();

		var plan =
			service.BuildPlan(
				temp.Path,
				"Installed games",
				"Game",
				"SiNNiC",
				[
					"Alfran",
					"SiNNiC"
				]
			);

		Assert.True(
			plan.AllIdentical
		);

		Assert.Empty(
			plan.Differences
		);

		Assert.Equal(
			2,
			plan.Candidates.Count
		);
	}

	[Fact]
	public void BuildPlan_DivergentAliases_ListaDiferencas()
	{
		using var temp =
			new TemporaryDirectory();

		var gameRoot =
			CreateGame(
				temp.Path,
				"Game"
			);

		WriteSave(
			gameRoot,
			"Alfran",
			"save.dat",
			"old"
		);

		WriteSave(
			gameRoot,
			"SiNNiC",
			"save.dat",
			"new"
		);

		WriteSave(
			gameRoot,
			"SiNNiC",
			"only-new.dat",
			"extra"
		);

		var service =
			new ProfileAliasRepairService();

		var plan =
			service.BuildPlan(
				temp.Path,
				"Installed games",
				"Game",
				"SiNNiC",
				[
					"Alfran",
					"SiNNiC"
				]
			);

		Assert.False(
			plan.AllIdentical
		);

		Assert.Contains(
			plan.Differences,
			item =>
				item.RelativePath ==
					"save.dat" &&
				item.Kind ==
					"Changed"
		);

		Assert.Contains(
			plan.Differences,
			item =>
				item.RelativePath ==
					"only-new.dat"
		);
	}

	[Fact]
	public void Execute_IdenticalAliases_ConsolidaComBackup()
	{
		using var temp =
			new TemporaryDirectory();

		using var backup =
			new TemporaryDirectory();

		var gameRoot =
			CreateGame(
				temp.Path,
				"Game"
			);

		WriteSave(
			gameRoot,
			"Alfran",
			"save.dat",
			"same"
		);

		WriteSave(
			gameRoot,
			"SiNNiC",
			"save.dat",
			"same"
		);

		var service =
			new ProfileAliasRepairService();

		var plan =
			service.BuildPlan(
				temp.Path,
				"Installed games",
				"Game",
				"SiNNiC",
				[
					"Alfran",
					"SiNNiC"
				]
			);

		var result =
			service.Execute(
				plan,
				"SiNNiC",
				backup.Path
			);

		Assert.True(
			Directory.Exists(
				Path.Combine(
					gameRoot,
					"SiNNiC"
				)
			)
		);

		Assert.False(
			Directory.Exists(
				Path.Combine(
					gameRoot,
					"Alfran"
				)
			)
		);

		Assert.Equal(
			"same",
			File.ReadAllText(
				Path.Combine(
					gameRoot,
					"SiNNiC",
					"save.dat"
				)
			)
		);

		Assert.True(
			File.Exists(
				Path.Combine(
					result.BackupPath,
					"metadata.json"
				)
			)
		);

		Assert.Equal(
			2,
			Directory.GetDirectories(
				Path.Combine(
					result.BackupPath,
					"aliases"
				)
			)
				.Length
		);
	}

	[Fact]
	public void Execute_DivergentAliases_PreservaVersaoEscolhidaInteira()
	{
		using var temp =
			new TemporaryDirectory();

		using var backup =
			new TemporaryDirectory();

		var gameRoot =
			CreateGame(
				temp.Path,
				"Game"
			);

		WriteSave(
			gameRoot,
			"Alfran",
			"save.dat",
			"switch-like"
		);

		WriteSave(
			gameRoot,
			"Alfran",
			"only-alfran.dat",
			"keep"
		);

		WriteSave(
			gameRoot,
			"SiNNiC",
			"save.dat",
			"eden-like"
		);

		WriteSave(
			gameRoot,
			"SiNNiC",
			"only-sinnic.dat",
			"discard"
		);

		var service =
			new ProfileAliasRepairService();

		var plan =
			service.BuildPlan(
				temp.Path,
				"Installed games",
				"Game",
				"SiNNiC",
				[
					"Alfran",
					"SiNNiC"
				]
			);

		var result =
			service.Execute(
				plan,
				"Alfran",
				backup.Path
			);

		var canonical =
			Path.Combine(
				gameRoot,
				"SiNNiC"
			);

		Assert.Equal(
			"switch-like",
			File.ReadAllText(
				Path.Combine(
					canonical,
					"save.dat"
				)
			)
		);

		Assert.True(
			File.Exists(
				Path.Combine(
					canonical,
					"only-alfran.dat"
				)
			)
		);

		Assert.False(
			File.Exists(
				Path.Combine(
					canonical,
					"only-sinnic.dat"
				)
			)
		);

		Assert.Equal(
			2,
			result.FileCount
		);
	}

	[Fact]
	public void BuildPlan_CanonicalExistenteForaDosAliases_Bloqueia()
	{
		using var temp =
			new TemporaryDirectory();

		var gameRoot =
			CreateGame(
				temp.Path,
				"Game"
			);

		WriteSave(
			gameRoot,
			"OldA",
			"a.dat",
			"a"
		);

		WriteSave(
			gameRoot,
			"OldB",
			"b.dat",
			"b"
		);

		WriteSave(
			gameRoot,
			"Canonical",
			"c.dat",
			"c"
		);

		var service =
			new ProfileAliasRepairService();

		Assert.Throws<InvalidOperationException>(
			() =>
				service.BuildPlan(
					temp.Path,
					"Installed games",
					"Game",
					"Canonical",
					[
						"OldA",
						"OldB"
					]
				)
		);
	}

	private static string CreateGame(
		string root,
		string game
	)
	{
		var path =
			Path.Combine(
				root,
				"Installed games",
				game
			);

		Directory.CreateDirectory(
			path
		);

		return path;
	}

	private static void WriteSave(
		string gameRoot,
		string alias,
		string relativePath,
		string content
	)
	{
		var path =
			Path.Combine(
				gameRoot,
				alias,
				relativePath
			);

		Directory.CreateDirectory(
			Path.GetDirectoryName(
				path
			)!
		);

		File.WriteAllText(
			path,
			content
		);
	}

	private sealed class TemporaryDirectory
		: IDisposable
	{
		public string Path
		{
			get;
		}

		public TemporaryDirectory()
		{
			Path =
				Directory.CreateTempSubdirectory(
					"SsaAliasRepair-"
				)
				.FullName;
		}

		public void Dispose()
		{
			if (Directory.Exists(
				Path
			))
			{
				Directory.Delete(
					Path,
					recursive: true
				);
			}
		}
	}
}

