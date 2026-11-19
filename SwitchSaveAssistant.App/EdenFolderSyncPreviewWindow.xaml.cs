using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Emulators;
using System.IO;
using System.Windows;

namespace SwitchSaveAssistant.App;

public partial class EdenFolderSyncPreviewWindow : Window
{
	private readonly EdenFolderSyncService _service =
		new();

	private readonly EdenFolderRecencyService _recencyService =
		new();

	private readonly string _gameName;
	private readonly string _profileName;
	private readonly string _edenPath;
	private readonly string _folderPath;

	public EdenFolderSyncPlan? SelectedPlan
	{
		get;
		private set;
	}

	public EdenFolderSyncPreviewWindow(
		string gameName,
		string profileName,
		string edenPath,
		string folderPath,
		EdenFolderSyncDirection? initialDirection
	)
	{
		_gameName = gameName;
		_profileName = profileName;
		_edenPath = edenPath;
		_folderPath = folderPath;

		InitializeComponent();

		var availableHeight =
			SystemParameters.WorkArea.Height;

		MaxHeight =
			Math.Max(
				600,
				availableHeight -
					12
			);

		Height =
			Math.Min(
				880,
				MaxHeight
			);

		TitleText.Text = gameName;
		ProfileText.Text = LocalizationService.Format("Preview_ProfileValue", profileName);
		if (profileName.Contains(
			"Device",
			StringComparison.OrdinalIgnoreCase
		))
		{
			ProfileText.Text =
				LocalizationService.Format("Preview_ProfileValue", LocalizationService.Get("Common_DeviceShared"));

			ProfileText.ToolTip =
				LocalizationService.Get("Preview_DeviceTooltip") +
				string.Empty;
		}

		EdenToFolderRadio.IsEnabled = Directory.Exists(edenPath);
		FolderToEdenRadio.IsEnabled = Directory.Exists(folderPath);

		ShowRecency();

		if (initialDirection == EdenFolderSyncDirection.EdenToFolder)
		{
			EdenToFolderRadio.IsChecked = true;
		}
		else if (initialDirection == EdenFolderSyncDirection.FolderToEden)
		{
			FolderToEdenRadio.IsChecked = true;
		}
		else
		{
			ChangeSummaryText.Text = LocalizationService.Get("Preview_ChooseDirection");
		}
	}

	private void ShowRecency()
	{
		var info =
			_recencyService.Compare(
				_edenPath,
				_folderPath
			);

		EdenDateText.Text = FormatDate(info.EdenLatestUtc);
		FolderDateText.Text = FormatDate(info.FolderLatestUtc);

		EdenNewestBadge.Visibility =
			info.State == EdenFolderRecencyState.EdenNewer
				? Visibility.Visible
				: Visibility.Collapsed;

		FolderNewestBadge.Visibility =
			info.State == EdenFolderRecencyState.FolderNewer
				? Visibility.Visible
				: Visibility.Collapsed;

		SuggestionText.Text =
			info.State switch
			{
				EdenFolderRecencyState.EdenNewer =>
					LocalizationService.Get("Preview_RecencyEdenNewer") +
					string.Empty,

				EdenFolderRecencyState.FolderNewer =>
					LocalizationService.Get("Preview_RecencyFolderNewer") +
					string.Empty,

				EdenFolderRecencyState.SameTime =>
					LocalizationService.Get("Preview_RecencySame") +
					string.Empty,

				_ =>
					LocalizationService.Get("Preview_RecencyUnknown") +
					string.Empty
			};
	}

	private static string FormatDate(DateTime? utc)
	{
		return utc is null
			? LocalizationService.Get("Common_NoDate")
			: utc.Value
				.ToLocalTime()
				.ToString("dd/MM/yyyy HH:mm:ss");
	}

	private void Direction_Changed(
		object sender,
		RoutedEventArgs e
	)
	{
		if (!IsInitialized)
		{
			return;
		}

		try
		{
			var direction =
				EdenToFolderRadio.IsChecked == true
					? EdenFolderSyncDirection.EdenToFolder
					: EdenFolderSyncDirection.FolderToEden;

			var plan =
				_service.BuildPlan(
					direction,
					_gameName,
					_profileName,
					_edenPath,
					_folderPath
				);

			SelectedPlan = plan;
			SourcePathText.Text = plan.SourcePath;
			DestinationPathText.Text = plan.DestinationPath;
			ChangesList.ItemsSource = plan.Changes;

			ChangeSummaryText.Text =
				plan.Changes.Count == 0
					? LocalizationService.Get("Preview_NoDifferences")
					: LocalizationService.Format("Preview_ChangesCount", plan.Changes.Count);

			SafetyText.Text =
				plan.DestinationExists
					? LocalizationService.Get("Preview_DestinationExistsSafety")
					: LocalizationService.Get("Preview_DestinationMissingSafety");

			ConfirmButton.IsEnabled = true;
		}
		catch (Exception ex)
		{
			SelectedPlan = null;
			ConfirmButton.IsEnabled = false;
			ChangesList.ItemsSource = null;
			ChangeSummaryText.Text = ex.Message;
		}
	}

	private void Confirm_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (SelectedPlan is null)
		{
			return;
		}

		DialogResult = true;
	}
}





















