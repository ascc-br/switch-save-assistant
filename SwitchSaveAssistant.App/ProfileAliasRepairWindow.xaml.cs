using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Profiles;
using System.Windows;
using System.Windows.Controls;

namespace SwitchSaveAssistant.App;

public partial class ProfileAliasRepairWindow : Window
{
	private readonly string _folderRoot;
	private readonly string _category;
	private readonly string _gameName;
	private readonly string _logicalPlayerName;
	private readonly string[] _aliases;

	private readonly ProfileAliasRepairService
		_service =
			new();

	private ProfileAliasRepairPlan?
		_plan;

	public ProfileAliasRepairWindow(
		string folderRoot,
		string category,
		string gameName,
		string logicalPlayerName,
		IEnumerable<string> aliases
	)
	{
		InitializeComponent();

		_folderRoot =
			folderRoot;

		_category =
			category;

		_gameName =
			gameName;

		_logicalPlayerName =
			logicalPlayerName;

		_aliases =
			aliases
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();
	}

	private async void Window_Loaded(
		object sender,
		RoutedEventArgs e
	)
	{
		ContextText.Text =
			LocalizationService.Format(
				"AliasRepair_Context",
				_gameName,
				_logicalPlayerName
			);

		ProgressText.Text =
			LocalizationService.Get(
				"AliasRepair_Analyzing"
			);

		try
		{
			var plan =
				await Task.Run(
					() =>
						_service.BuildPlan(
							_folderRoot,
							_category,
							_gameName,
							_logicalPlayerName,
							_aliases
						)
				);

			_plan =
				plan;

			CandidatesGrid.ItemsSource =
				plan.Candidates
					.Select(
						candidate =>
							new CandidateRow(
								candidate.Alias,
								candidate.FileCount,
								FormatBytes(
									candidate.TotalBytes
								),
								candidate.LastWriteTime
									?.ToLocalTime()
									.ToString(
										"dd/MM/yyyy HH:mm"
									) ??
								"—"
							)
					)
					.ToArray();

			SourceAliasCombo.ItemsSource =
				plan.Candidates
					.Select(
						candidate =>
							candidate.Alias
					)
					.ToArray();

			if (plan.AllIdentical)
			{
				SummaryText.Text =
					LocalizationService.Get(
						"AliasRepair_Identical"
					);

				DifferencesHeaderText.Text =
					LocalizationService.Get(
						"AliasRepair_NoDifferences"
					);

				DifferencesGrid.ItemsSource =
					Array.Empty<DifferenceRow>();

				var preferred =
					plan.Candidates.FirstOrDefault(
						candidate =>
							string.Equals(
								candidate.Alias,
								plan.LogicalPlayerName,
								StringComparison.OrdinalIgnoreCase
							)
					)
					?.Alias ??
					plan.Candidates[0].Alias;

				SourceAliasCombo.SelectedItem =
					preferred;
			}
			else
			{
				SummaryText.Text =
					LocalizationService.Format(
						"AliasRepair_Divergent",
						plan.Differences.Count
					);

				DifferencesHeaderText.Text =
					LocalizationService.Format(
						"AliasRepair_DifferencesCount",
						plan.Differences.Count
					);

				DifferencesGrid.ItemsSource =
					plan.Differences
						.Take(
							250
						)
						.Select(
							difference =>
								new DifferenceRow(
									$"{difference.ReferenceAlias} ↔ {difference.ComparedAlias}",
									TranslateDifferenceKind(
										difference.Kind
									),
									difference.RelativePath
								)
						)
						.ToArray();

				SourceAliasCombo.SelectedIndex =
					-1;
			}

			ProgressText.Text =
				LocalizationService.Format(
					"AliasRepair_BackupDestination",
					ProfileAliasRepairService.GetDefaultBackupRoot()
				);
		}
		catch (Exception ex)
		{
			ProgressText.Text =
				ex.Message;

			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Error
			);
		}

		UpdateRepairButton();
	}

	private void SourceAliasCombo_SelectionChanged(
		object sender,
		SelectionChangedEventArgs e
	)
	{
		UpdateRepairButton();
	}

	private async void Repair_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (
			_plan is null ||
			SourceAliasCombo.SelectedItem
				is not string selectedAlias
		)
		{
			return;
		}

		var confirmation =
			LocalizationService.Format(
				"AliasRepair_Confirm",
				selectedAlias,
				_plan.LogicalPlayerName,
				_plan.GameName
			);

		if (MessageBox.Show(
				this,
				confirmation,
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning
			) !=
			MessageBoxResult.Yes)
		{
			return;
		}

		SetBusy(
			true
		);

		var progress =
			new Progress<ProfileAliasRepairProgress>(
				update =>
					ProgressText.Text =
						TranslateProgress(
							update
						)
			);

		try
		{
			var result =
				await Task.Run(
					() =>
						_service.Execute(
							_plan,
							selectedAlias,
							progress:
								update =>
									((IProgress<ProfileAliasRepairProgress>)progress)
										.Report(
											update
										)
						)
				);

			MessageBox.Show(
				this,
				LocalizationService.Format(
					"AliasRepair_Success",
					result.CanonicalPath,
					result.BackupPath
				),
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);

			DialogResult =
				true;
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				LocalizationService.Format(
					"AliasRepair_Failed",
					ex.Message
				),
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Error
			);

			ProgressText.Text =
				LocalizationService.Get(
					"AliasRepair_FailedShort"
				);

			SetBusy(
				false
			);
		}
	}

	private void Cancel_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		Close();
	}

	private void UpdateRepairButton()
	{
		RepairButton.IsEnabled =
			_plan is not null &&
			SourceAliasCombo.SelectedItem
				is string;
	}

	private void SetBusy(
		bool busy
	)
	{
		RepairButton.IsEnabled =
			!busy &&
			_plan is not null &&
			SourceAliasCombo.SelectedItem
				is string;

		CancelButton.IsEnabled =
			!busy;

		SourceAliasCombo.IsEnabled =
			!busy;

		CandidatesGrid.IsEnabled =
			!busy;

		DifferencesGrid.IsEnabled =
			!busy;
	}

	private static string TranslateDifferenceKind(
		string kind
	)
	{
		return kind switch
			{
				"OnlyReference" =>
					LocalizationService.Get(
						"AliasRepair_OnlyReference"
					),

				"OnlyCompared" =>
					LocalizationService.Get(
						"AliasRepair_OnlyCompared"
					),

				"Changed" =>
					LocalizationService.Get(
						"AliasRepair_Changed"
					),

				_ =>
					kind
			};
	}

	private static string TranslateProgress(
		ProfileAliasRepairProgress progress
	)
	{
		var alias =
			string.IsNullOrWhiteSpace(
				progress.Alias
			)
				? string.Empty
				: " • " +
					progress.Alias;

		var key =
			progress.Stage switch
				{
					ProfileAliasRepairStage.CreatingBackup =>
						"AliasRepair_ProgressBackup",

					ProfileAliasRepairStage.ValidatingBackup =>
						"AliasRepair_ProgressValidateBackup",

					ProfileAliasRepairStage.CreatingStaging =>
						"AliasRepair_ProgressStaging",

					ProfileAliasRepairStage.ValidatingStaging =>
						"AliasRepair_ProgressValidateStaging",

					ProfileAliasRepairStage.ReplacingAliases =>
						"AliasRepair_ProgressReplace",

					ProfileAliasRepairStage.ValidatingFinal =>
						"AliasRepair_ProgressFinal",

					ProfileAliasRepairStage.RestoringRollback =>
						"AliasRepair_ProgressRollback",

					_ =>
						"AliasRepair_Analyzing"
				};

		return LocalizationService.Get(
				key
			) +
			alias;
	}

	private static string FormatBytes(
		long bytes
	)
	{
		if (bytes >=
			1024L *
			1024L *
			1024L)
		{
			return
				$"{bytes / (1024d * 1024d * 1024d):0.##} GB";
		}

		if (bytes >=
			1024L *
			1024L)
		{
			return
				$"{bytes / (1024d * 1024d):0.##} MB";
		}

		if (bytes >=
			1024L)
		{
			return
				$"{bytes / 1024d:0.##} KB";
		}

		return
			$"{bytes} B";
	}

	private sealed record CandidateRow(
		string Alias,
		int Files,
		string Size,
		string LastWrite
	);

	private sealed record DifferenceRow(
		string Pair,
		string Kind,
		string Path
	);
}

