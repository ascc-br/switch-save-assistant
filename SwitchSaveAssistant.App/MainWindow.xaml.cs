using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Settings;
using System.Windows;

namespace SwitchSaveAssistant.App;

public partial class MainWindow : Window
{
	private readonly AssistantSettingsService
		_settingsService =
			new();

	public MainWindow()
	{
		InitializeComponent();
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
}

