using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Backups;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SwitchSaveAssistant.App;

public partial class SafetyBackupsWindow : Window
{
	private readonly SafetyBackupCatalogService
		_catalog =
			new();

	private readonly SafetyBackupRestoreService
		_restore =
			new();

	private List<BackupRow>
		_rows =
			new();

	public SafetyBackupsWindow()
	{
		InitializeComponent();

		Loaded +=
			(_, _) =>
				RefreshList();
	}

	private void Refresh_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		RefreshList();
	}

	private void RefreshList()
	{
		var entries =
			_catalog.List();

		_rows =
			entries
				.Select(
					entry =>
						new BackupRow(
							entry
						)
				)
				.ToList();

		BackupsGrid.ItemsSource =
			_rows;

		var restorable =
			_rows.Count(
				row =>
					row.Entry.IsRestorable
			);

		var legacy =
			_rows.Count -
			restorable;

		SummaryText.Text =
			LocalizationService.Format("Backups_SummaryTotal", _rows.Count) +
			LocalizationService.Format("Backups_SummaryRestorable", restorable) +
			LocalizationService.Format("Backups_SummaryLegacy", legacy);

		FooterText.Text =
			LocalizationService.Format("Backups_FolderPath", _catalog.DefaultRoot);

		UpdateSelection();
	}

	private void BackupsGrid_SelectionChanged(
		object sender,
		SelectionChangedEventArgs e
	)
	{
		UpdateSelection();
	}

	private void UpdateSelection()
	{
		if (BackupsGrid.SelectedItem
			is not BackupRow row)
		{
			DetailsText.Text =
				LocalizationService.Get("Backups_SelectBackup");

			StatusText.Text =
				LocalizationService.Get("Backups_NoneSelected");

			RestoreButton.IsEnabled =
				false;

			OpenSelectedButton.IsEnabled =
				false;

			return;
		}

		var entry =
			row.Entry;

		DetailsText.Text =
			LocalizationService.Format("Backups_GameLine", entry.GameName) +
			LocalizationService.Format("Backups_ProfileLine", entry.ProfileName) +
			LocalizationService.Format("Backups_DateLine", entry.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")) +
			LocalizationService.Format("Backups_DestinationLine", entry.DestinationSide) +
			LocalizationService.Format("Backups_FilesLine", entry.FileCount) +
			LocalizationService.Format("Backups_SizeLine", FormatBytes(entry.TotalBytes)) +
			LocalizationService.Format("Backups_BackupBlock", entry.BackupPayloadPath) +
			(
				entry.OriginalDestinationPath is null
					? LocalizationService.Get("Backups_OriginalUnknown")
					: LocalizationService.Format("Backups_OriginalBlock", entry.OriginalDestinationPath)
			);

		StatusText.Text =
			entry.IsRestorable
				? LocalizationService.Get("Backups_RestorableDetail")
				: LocalizationService.Get("Backups_LegacyDetail");

		RestoreButton.IsEnabled =
			entry.IsRestorable;

		OpenSelectedButton.IsEnabled =
			Directory.Exists(
				entry.BackupPayloadPath
			);
	}

	private async void Restore_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (BackupsGrid.SelectedItem
			is not BackupRow row)
		{
			return;
		}

		var entry =
			row.Entry;

		if (!entry.IsRestorable)
		{
			return;
		}

		if (string.Equals(
				entry.DestinationSide,
				"Eden",
				StringComparison.OrdinalIgnoreCase
			) &&
			IsEdenRunning())
		{
			MessageBox.Show(
				this,
				LocalizationService.Get("Backups_CloseEdenRestore"),
				LocalizationService.Get("Emulator_EdenOpenTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		SafetyBackupRestorePlan plan;

		try
		{
			plan =
				_restore.BuildPlan(
					entry
				);
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get("Backups_NotRestorableTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		var preview =
			string.Join(
				"\n",
				plan.Changes
					.Take(
						8
					)
					.Select(
						change =>
							$"• {change.Operation}: {change.RelativePath}"
					)
			);

		if (plan.Changes.Count >
			8)
		{
			preview +=
				LocalizationService.Format("Backups_MoreChanges", plan.Changes.Count - 8);
		}

		var confirmation =
			LocalizationService.Get("Backups_RestoreQuestion") +
			LocalizationService.Format("Backups_GameLine", entry.GameName) +
			LocalizationService.Format("Backups_ProfileLine", entry.ProfileName) +
			LocalizationService.Format("Backups_BackupDateLine", entry.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")) +
			LocalizationService.Format("Emulator_DestinationBlock", entry.OriginalDestinationPath ?? string.Empty) + "\n\n" +
			LocalizationService.Format("Backups_PlannedChangesLine", plan.Changes.Count) +
			(
				string.IsNullOrWhiteSpace(
					preview
				)
					? LocalizationService.Get("Backups_NoDifferencesDetected")
					: preview
			) +
			LocalizationService.Get("Backups_BeforeReplaceRollback");

		if (MessageBox.Show(
				this,
				confirmation,
				LocalizationService.Get("Backups_ConfirmRestoreTitle"),
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning
			) !=
			MessageBoxResult.Yes)
		{
			return;
		}

		RestoreButton.IsEnabled =
			false;

		var rollbackRoot =
			Path.Combine(
				Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData
				),
				"SwitchSaveAssistant",
				"SafetyBackups",
				"RestoreRollback"
			);

		IProgress<string> progress =
			new Progress<string>(
				message =>
					StatusText.Text =
					LocalizationService.TranslateRuntimeMessage(message)
			);

		try
		{
			var result =
				await Task.Run(
					() =>
						_restore.Execute(
							plan,
							rollbackRoot,
							message =>
								progress.Report(
									message
								)
						)
				);

			var message =
				LocalizationService.Get("Backups_RestoreSuccess") + "\n\n" +
				LocalizationService.Format("Emulator_DestinationBlock", result.DestinationPath) + "\n\n" +
				LocalizationService.Format("Emulator_VerifiedFiles", result.FileCount);

			if (!string.IsNullOrWhiteSpace(
				result.RollbackPath
			))
			{
				message +=
					"\n\n" + LocalizationService.Format("Backups_RollbackBlock", result.RollbackPath);
			}

			MessageBox.Show(
				this,
				message,
				LocalizationService.Get("Backups_RestoreCompletedTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);

			RefreshList();
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				LocalizationService.Get("Backups_RestoreFailedPrefix") + "\n\n" +
				ex.Message,
				LocalizationService.Get("Backups_RestoreFailedTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Error
			);

			UpdateSelection();
		}
	}

	private void OpenFolder_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		OpenExplorer(
			_catalog.DefaultRoot
		);
	}

	private void OpenSelected_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (BackupsGrid.SelectedItem
			is BackupRow row)
		{
			OpenExplorer(
				row.Entry.BackupPayloadPath
			);
		}
	}

	private static void OpenExplorer(
		string path
	)
	{
		Directory.CreateDirectory(
			path
		);

		Process.Start(
			new ProcessStartInfo
				{
					FileName =
						"explorer.exe",

					Arguments =
						$"/n,\"{path}\"",

					UseShellExecute =
						true
				}
		);
	}

	private static bool IsEdenRunning()
	{
		try
		{
			return Process
				.GetProcesses()
				.Any(
					process =>
						string.Equals(
							process.ProcessName,
							"eden",
							StringComparison.OrdinalIgnoreCase
						)
				);
		}
		catch
		{
			return false;
		}
	}

	private static string FormatBytes(
		long bytes
	)
	{
		string[] units =
		[
			"B",
			"KB",
			"MB",
			"GB"
		];

		double value =
			bytes;

		var unit =
			0;

		while (value >=
				1024 &&
			unit <
				units.Length -
					1)
		{
			value /=
				1024;

			unit++;
		}

		return unit ==
			0
				? $"{bytes:N0} B"
				: $"{value:N1} {units[unit]}";
	}

	private sealed class BackupRow
	{
		public SafetyBackupEntry Entry
		{
			get;
		}

		public string CreatedLocalText =>
			Entry.CreatedAtUtc
				.ToLocalTime()
				.ToString(
					"dd/MM/yyyy HH:mm"
				);

		public string GameName =>
			Entry.GameName;

		public string ProfileName =>
			Entry.ProfileName;

		public string DestinationSide =>
			Entry.DestinationSide;

		public string Status =>
			Entry.IsRestorable
				? LocalizationService.Get(
					"Backups_StatusRestorable"
				)
				: LocalizationService.Get(
					"Backups_StatusLegacy"
				);

		public BackupRow(
			SafetyBackupEntry entry
		)
		{
			Entry =
				entry;
		}
	}
}






































