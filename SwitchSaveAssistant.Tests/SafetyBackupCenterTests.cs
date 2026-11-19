using SwitchSaveAssistant.Core.Backups;
using SwitchSaveAssistant.Core.Emulators;

namespace SwitchSaveAssistant.Tests;

public sealed class SafetyBackupCenterTests
{
	[Fact]
	public void Register_CriaMetadataEListaBackupRestauravel()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaBackupCatalog-"
			)
			.FullName;

		try
		{
			var payload =
				Path.Combine(
					root,
					"20261118-010000-000-12345678",
					"Jogo",
					"Perfil",
					"destination"
				);

			Directory.CreateDirectory(
				payload
			);

			File.WriteAllText(
				Path.Combine(
					payload,
					"save.dat"
				),
				"backup"
			);

			var destination =
				Path.Combine(
					root,
					"destino-real"
				);

			var plan =
				new EdenFolderSyncPlan(
					EdenFolderSyncDirection.FolderToEden,
					"Jogo",
					"Perfil",
					@"C:\origem",
					destination,
					true,
					Array.Empty<EdenFolderSyncChange>(),
					1,
					1
				);

			var result =
				new EdenFolderSyncResult(
					destination,
					payload,
					1
				);

			var service =
				new SafetyBackupCatalogService();

			var registered =
				service.Register(
					plan,
					result
				);

			Assert.NotNull(
				registered
			);

			Assert.True(
				File.Exists(
					Path.Combine(
						registered!.ContainerPath,
						"metadata.json"
					)
				)
			);

			var list =
				service.List(
					root
				);

			var item =
				Assert.Single(
					list
				);

			Assert.True(
				item.IsRestorable
			);

			Assert.Equal(
				destination,
				item.OriginalDestinationPath
			);

			Assert.Equal(
				"Eden",
				item.DestinationSide
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
	public void BackupAntigoSemMetadata_EhLegadoNaoRestauravel()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaBackupLegacy-"
			)
			.FullName;

		try
		{
			var payload =
				Path.Combine(
					root,
					"antigo",
					"Jogo",
					"Perfil",
					"destination"
				);

			Directory.CreateDirectory(
				payload
			);

			File.WriteAllText(
				Path.Combine(
					payload,
					"save.dat"
				),
				"backup"
			);

			var service =
				new SafetyBackupCatalogService();

			var item =
				Assert.Single(
					service.List(
						root
					)
				);

			Assert.False(
				item.IsRestorable
			);

			Assert.Equal(
				"Legado",
				item.DestinationSide
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
	public void Restore_SubstituiDestinoECriaRollback()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaBackupRestore-"
			)
			.FullName;

		try
		{
			var backupPayload =
				Path.Combine(
					root,
					"backup"
				);

			var destination =
				Path.Combine(
					root,
					"destination"
				);

			var rollbackRoot =
				Path.Combine(
					root,
					"rollback"
				);

			Directory.CreateDirectory(
				backupPayload
			);

			Directory.CreateDirectory(
				destination
			);

			File.WriteAllText(
				Path.Combine(
					backupPayload,
					"save.dat"
				),
				"versão antiga"
			);

			File.WriteAllText(
				Path.Combine(
					destination,
					"save.dat"
				),
				"versão atual"
			);

			var entry =
				new SafetyBackupEntry(
					"id",
					DateTimeOffset.UtcNow,
					"Jogo",
					"Perfil",
					"FolderToEden",
					"Eden",
					destination,
					backupPayload,
					root,
					1,
					13,
					true,
					"Restaurável"
				);

			var service =
				new SafetyBackupRestoreService();

			var plan =
				service.BuildPlan(
					entry
				);

			Assert.Contains(
				plan.Changes,
				change =>
					change.Operation ==
						"Alterar" &&
					change.RelativePath ==
						"save.dat"
			);

			var result =
				service.Execute(
					plan,
					rollbackRoot
				);

			Assert.Equal(
				"versão antiga",
				File.ReadAllText(
					Path.Combine(
						destination,
						"save.dat"
					)
				)
			);

			Assert.NotNull(
				result.RollbackPath
			);

			Assert.Equal(
				"versão atual",
				File.ReadAllText(
					Path.Combine(
						result.RollbackPath!,
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
	public void Restore_DestinoAusente_CriaNovoDestino()
	{
		var root =
			Directory.CreateTempSubdirectory(
				"SsaBackupRestoreNew-"
			)
			.FullName;

		try
		{
			var backupPayload =
				Path.Combine(
					root,
					"backup"
				);

			var destination =
				Path.Combine(
					root,
					"novo-destino"
				);

			Directory.CreateDirectory(
				backupPayload
			);

			File.WriteAllText(
				Path.Combine(
					backupPayload,
					"save.dat"
				),
				"restaurado"
			);

			var entry =
				new SafetyBackupEntry(
					"id",
					DateTimeOffset.UtcNow,
					"Jogo",
					"Perfil",
					"EdenToFolder",
					"Folder",
					destination,
					backupPayload,
					root,
					1,
					10,
					true,
					"Restaurável"
				);

			var service =
				new SafetyBackupRestoreService();

			var result =
				service.Execute(
					service.BuildPlan(
						entry
					),
					Path.Combine(
						root,
						"rollback"
					)
				);

			Assert.Null(
				result.RollbackPath
			);

			Assert.Equal(
				"restaurado",
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
}
