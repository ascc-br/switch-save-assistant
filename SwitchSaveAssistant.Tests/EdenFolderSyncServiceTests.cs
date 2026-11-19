using SwitchSaveAssistant.Core.Emulators;

namespace SwitchSaveAssistant.Tests;

public sealed class EdenFolderSyncServiceTests
{
	[Fact]
	public void BuildPlan_ListaAdicionarAlterarRemover()
	{
		var source =
			Directory.CreateTempSubdirectory(
				"EdenSyncSource-"
			)
			.FullName;

		var destination =
			Directory.CreateTempSubdirectory(
				"EdenSyncDestination-"
			)
			.FullName;

		try
		{
			File.WriteAllText(
				Path.Combine(
					source,
					"novo.dat"
				),
				"novo"
			);

			File.WriteAllText(
				Path.Combine(
					source,
					"alterado.dat"
				),
				"origem"
			);

			File.WriteAllText(
				Path.Combine(
					destination,
					"alterado.dat"
				),
				"destino"
			);

			File.WriteAllText(
				Path.Combine(
					destination,
					"remover.dat"
				),
				"antigo"
			);

			var service =
				new EdenFolderSyncService();

			var plan =
				service.BuildPlan(
					EdenFolderSyncDirection.EdenToFolder,
					"Jogo",
					"Perfil",
					source,
					destination
				);

			Assert.Contains(
				plan.Changes,
				change =>
					change.Operation ==
						"Adicionar" &&
					change.RelativePath ==
						"novo.dat"
			);

			Assert.Contains(
				plan.Changes,
				change =>
					change.Operation ==
						"Alterar" &&
					change.RelativePath ==
						"alterado.dat"
			);

			Assert.Contains(
				plan.Changes,
				change =>
					change.Operation ==
						"Remover" &&
					change.RelativePath ==
						"remover.dat"
			);
		}
		finally
		{
			Directory.Delete(
				source,
				true
			);

			Directory.Delete(
				destination,
				true
			);
		}
	}

	[Fact]
	public void Execute_DestinoNovo_CopiaEValida()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenSyncNew-"
			)
			.FullName;

		try
		{
			var source =
				Path.Combine(
					root,
					"source"
				);

			var destination =
				Path.Combine(
					root,
					"destination"
				);

			var backups =
				Path.Combine(
					root,
					"backups"
				);

			Directory.CreateDirectory(
				source
			);

			File.WriteAllText(
				Path.Combine(
					source,
					"save.dat"
				),
				"conteúdo"
			);

			var service =
				new EdenFolderSyncService();

			var plan =
				service.BuildPlan(
					EdenFolderSyncDirection.EdenToFolder,
					"Jogo",
					"Perfil",
					source,
					destination
				);

			var result =
				service.Execute(
					plan,
					backups
				);

			Assert.True(
				File.Exists(
					Path.Combine(
						destination,
						"save.dat"
					)
				)
			);

			Assert.Null(
				result.BackupPath
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
	public void Execute_DestinoExistente_CriaBackupAntesDeSubstituir()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenSyncBackup-"
			)
			.FullName;

		try
		{
			var source =
				Path.Combine(
					root,
					"source"
				);

			var destination =
				Path.Combine(
					root,
					"destination"
				);

			var backups =
				Path.Combine(
					root,
					"backups"
				);

			Directory.CreateDirectory(
				source
			);

			Directory.CreateDirectory(
				destination
			);

			File.WriteAllText(
				Path.Combine(
					source,
					"save.dat"
				),
				"novo"
			);

			File.WriteAllText(
				Path.Combine(
					destination,
					"save.dat"
				),
				"antigo"
			);

			var service =
				new EdenFolderSyncService();

			var plan =
				service.BuildPlan(
					EdenFolderSyncDirection.EdenToFolder,
					"Jogo",
					"Perfil",
					source,
					destination
				);

			var result =
				service.Execute(
					plan,
					backups
				);

			Assert.NotNull(
				result.BackupPath
			);

			Assert.Equal(
				"antigo",
				File.ReadAllText(
					Path.Combine(
						result.BackupPath!,
						"save.dat"
					)
				)
			);

			Assert.Equal(
				"novo",
				File.ReadAllText(
					Path.Combine(
						destination,
						"save.dat"
					)
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
	public void FolderParaEden_PreservaYuzuSaveSizeDoDestino()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"EdenSyncTechnical-"
			)
			.FullName;

		try
		{
			var folder =
				Path.Combine(
					root,
					"folder"
				);

			var eden =
				Path.Combine(
					root,
					"eden"
				);

			var backups =
				Path.Combine(
					root,
					"backups"
				);

			Directory.CreateDirectory(
				folder
			);

			Directory.CreateDirectory(
				eden
			);

			File.WriteAllText(
				Path.Combine(
					folder,
					"save.dat"
				),
				"novo"
			);

			File.WriteAllText(
				Path.Combine(
					eden,
					"save.dat"
				),
				"antigo"
			);

			File.WriteAllText(
				Path.Combine(
					eden,
					".yuzu_save_size"
				),
				"técnico"
			);

			var service =
				new EdenFolderSyncService();

			var plan =
				service.BuildPlan(
					EdenFolderSyncDirection.FolderToEden,
					"Jogo",
					"Perfil",
					eden,
					folder
				);

			service.Execute(
				plan,
				backups
			);

			Assert.Equal(
				"técnico",
				File.ReadAllText(
					Path.Combine(
						eden,
						".yuzu_save_size"
					)
				)
			);

			Assert.Equal(
				"novo",
				File.ReadAllText(
					Path.Combine(
						eden,
						"save.dat"
					)
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
