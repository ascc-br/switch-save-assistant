using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Settings;
using System.Windows;

namespace SwitchSaveAssistant.App;

public partial class App : Application
{
	protected override void OnStartup(
		StartupEventArgs e
	)
	{
		base.OnStartup(
			e
		);

		var settings =
			new AssistantSettingsService()
				.Load();

		LocalizationService.Apply(
			settings.Language
		);

		AppThemeService.Apply(
			settings.Theme
		);

		var window =
			new MainWindow();

		MainWindow =
			window;

		window.Show();
	}
}
