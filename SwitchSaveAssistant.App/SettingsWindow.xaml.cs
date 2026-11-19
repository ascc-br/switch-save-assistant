using Microsoft.Win32;
using System.IO;
using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using SwitchSaveAssistant.App.Updates;

namespace SwitchSaveAssistant.App;

public partial class SettingsWindow : Window
{
	private readonly AssistantSettingsService
		_settingsService =
			new();

	private AssistantSettings
		_settings;

	public SettingsWindow()
	{
		InitializeComponent();

		_settings =
			_settingsService.Load();

		LoadControls();
		Closed += (_, _) => _updatesCancellation.Cancel();
	}

	private void LoadControls()
	{
		CheckUpdatesCheckBox.IsChecked = _settings.CheckForUpdatesOnStartup;
		CurrentVersionText.Text = LocalizationService.Format("Updates_Current", AppUpdates.CurrentVersion);
		UpdateStatusText.Text = LocalizationService.Get("Updates_Ready");
		RefreshUpdateButton();
		FolderPathTextBox.Text =
			_settings.FolderPath ??
			string.Empty;

		SelectByTag(
			LanguageComboBox,
			LocalizationService.Normalize(
				_settings.Language
			)
		);

		SelectByTag(
			ThemeComboBox,
			AppThemeService.Normalize(
				_settings.Theme
			)
		);

		SelectByTag(
			DefaultEmulatorComboBox,
			NormalizeEmulator(
				_settings.DefaultEmulator
			)
		);
	}

	private void SelectFolder_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var dialog =
			new OpenFolderDialog
				{
					Title =
						LocalizationService.Get(
							"Settings_SelectFolder"
						),

					Multiselect =
						false
				};

		if (!string.IsNullOrWhiteSpace(
				FolderPathTextBox.Text
			) &&
			Directory.Exists(
				FolderPathTextBox.Text
			))
		{
			dialog.InitialDirectory =
				FolderPathTextBox.Text;
		}

		if (dialog.ShowDialog(
				this
			) !=
			true)
		{
			return;
		}

		FolderPathTextBox.Text =
			dialog.FolderName;
	}

	private void Save_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var language =
			GetSelectedTag(
				LanguageComboBox,
				"en-US"
			);

		var theme =
			GetSelectedTag(
				ThemeComboBox,
				"System"
			);

		var emulator =
			GetSelectedTag(
				DefaultEmulatorComboBox,
				"Automatic"
			);

		_settings =
			_settings with
				{
					FolderPath =
						string.IsNullOrWhiteSpace(
							FolderPathTextBox.Text
						)
							? null
							: FolderPathTextBox.Text,

					Language =
						language,

					Theme =
						theme,

					DefaultEmulator =
						emulator,

					CheckForUpdatesOnStartup = CheckUpdatesCheckBox.IsChecked == true
				};

		_settingsService.Save(
			_settings
		);

		LocalizationService.Apply(
			language
		);

		AppThemeService.Apply(
			theme
		);

		DialogResult =
			true;
	}

	private static string GetSelectedTag(
		ComboBox comboBox,
		string fallback
	)
	{
		return comboBox.SelectedItem
			is ComboBoxItem item &&
			item.Tag is string tag
				? tag
				: fallback;
	}

	private static void SelectByTag(
		ComboBox comboBox,
		string tag
	)
	{
		foreach (var candidate
			in comboBox.Items)
		{
			if (candidate
				is ComboBoxItem item &&
				string.Equals(
					item.Tag?.ToString(),
					tag,
					StringComparison.Ordinal
				))
			{
				comboBox.SelectedItem =
					item;

				return;
			}
		}

		comboBox.SelectedIndex =
			0;
	}

	private static string NormalizeEmulator(
		string? emulator
	)
	{
		return emulator switch
			{
				"Eden" =>
					"Eden",

				"Citron" =>
					"Citron",

				"Ryujinx" =>
					"Ryujinx",

				_ =>
					"Automatic"
			};
	}

    private readonly CancellationTokenSource _updatesCancellation = new();
    private bool _updatesBusy;

    private void RefreshUpdateButton()
    {
        InstallUpdateButton.Visibility = AppUpdates.Latest is null ? Visibility.Collapsed : Visibility.Visible;
        InstallUpdateButton.Content = LocalizationService.Get(AppUpdates.IsInstalled ? "Updates_Install" : "Updates_DownloadPortable");
        if (AppUpdates.Latest is { } update)
            UpdateStatusText.Text = LocalizationService.Format("Updates_Available", update.Version);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_updatesBusy) return;
        SetUpdatesBusy(true, "Updates_Checking");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_updatesCancellation.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var result = await AppUpdates.Service.CheckAsync(AppUpdates.CurrentVersion, AppUpdates.PackageKind, timeout.Token);
            AppUpdates.Latest = result.Update;
            UpdateStatusText.Text = LocalizationService.Get(result.HasPublishedPackages ? "Updates_CurrentLatest" : "Updates_NoReleases");
            RefreshUpdateButton();
        }
        catch (Exception) { if (!_updatesCancellation.IsCancellationRequested) UpdateStatusText.Text = LocalizationService.Get("Updates_CheckFailed"); }
        finally { SetUpdatesBusy(false); }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updatesBusy || AppUpdates.Latest is not { } update) return;
        var installed = AppUpdates.IsInstalled;
        if (MessageBox.Show(this, LocalizationService.Format(installed ? "Updates_ConfirmInstall" : "Updates_ConfirmPortable", update.Version),
            LocalizationService.Get("Updates_Title"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        SetUpdatesBusy(true, "Updates_Downloading");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_updatesCancellation.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwitchSaveAssistant", "Updates");
            var path = await AppUpdates.Service.DownloadAsync(update, root, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (installed)
            {
                if (Application.Current.Windows.OfType<Window>().Any(w => w != this && w != Application.Current.MainWindow && w.IsVisible))
                    throw new InvalidOperationException("Another application window is open.");
                _settingsService.Update(s => s with { CheckForUpdatesOnStartup = CheckUpdatesCheckBox.IsChecked == true });
                var start = new ProcessStartInfo(path) { UseShellExecute = true };
                start.ArgumentList.Add("/DIR=" + Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
                start.ArgumentList.Add("/NORESTART");
                start.ArgumentList.Add("/NORESTARTAPPLICATIONS");
                using var process = Process.Start(start) ?? throw new IOException("Installer did not start.");
                Application.Current.Shutdown();
            }
            else
            {
                Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path)!) { UseShellExecute = true });
                UpdateStatusText.Text = LocalizationService.Get("Updates_PortableReady");
            }
        }
        catch (Exception) { if (!_updatesCancellation.IsCancellationRequested) UpdateStatusText.Text = LocalizationService.Get("Updates_DownloadFailed"); }
        finally { SetUpdatesBusy(false); }
    }

    private void SetUpdatesBusy(bool busy, string? statusKey = null)
    {
        _updatesBusy = busy;
        CheckUpdatesButton.IsEnabled = !busy;
        InstallUpdateButton.IsEnabled = !busy;
        if (statusKey is not null) UpdateStatusText.Text = LocalizationService.Get(statusKey);
    }
}

