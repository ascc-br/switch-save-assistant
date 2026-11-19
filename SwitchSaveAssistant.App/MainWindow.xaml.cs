using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Settings;
using System.Windows;
using SwitchSaveAssistant.App.Updates;

namespace SwitchSaveAssistant.App;

public partial class MainWindow : Window
{
	private readonly AssistantSettingsService
		_settingsService =
			new();

	public MainWindow()
	{
		InitializeComponent();
		Loaded += CheckUpdatesOnStartup;
	}

	private void OpenProfiles_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var window =
			new PlayerProfilesWindow
				{
					Owner =
						this
				};

		window.ShowDialog();
	}
	private void OpenSettings_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var window =
			new SettingsWindow
				{
					Owner =
						this
				};

		window.ShowDialog();
	}

	private void OpenFolderSwitch_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var window =
			new FolderSwitchWindow
				{
					Owner =
						this
				};

		window.ShowDialog();
	}

	private void OpenEmulatorFolder_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var settings =
			_settingsService.Load();

		if (settings.DefaultEmulator
			is "Citron" or
				"Ryujinx")
		{
			MessageBox.Show(
				this,
				LocalizationService.Format(
					"Main_EmulatorPendingMessage",
					settings.DefaultEmulator
				),
				LocalizationService.Get(
					"Main_EmulatorPendingTitle"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);

			return;
		}

		var window =
			new EmulatorFolderWindow
				{
					Owner =
						this
				};

		window.ShowDialog();
	}

    private async void CheckUpdatesOnStartup(object sender, RoutedEventArgs e)
    {
        Loaded -= CheckUpdatesOnStartup;
        if (!_settingsService.Load().CheckForUpdatesOnStartup) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var result = await AppUpdates.Service.CheckAsync(AppUpdates.CurrentVersion, AppUpdates.PackageKind, timeout.Token);
            if (!IsLoaded || !_settingsService.Load().CheckForUpdatesOnStartup) return;
            AppUpdates.Latest = result.Update;
            if (result.Update is { } update)
            {
                UpdateBannerText.Text = LocalizationService.Format("Updates_Available", update.Version);
                UpdateBanner.Visibility = Visibility.Visible;
            }
        }
        catch (Exception) { /* Startup remains usable offline or when the API is unavailable. */ }
    }
}

