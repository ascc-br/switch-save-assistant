using SwitchSaveAssistant.App.Localization;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Profiles;
using SwitchSaveAssistant.Core.Settings;
using System.IO;
using System.Windows;

namespace SwitchSaveAssistant.App;

public partial class PlayerProfilesWindow : Window
{
	private readonly PlayerProfileLinkService
		_linkService =
			new();

	private readonly EdenProfileDiscoveryService
		_edenDiscoveryService =
			new();

	private readonly SwitchProfileDiscoveryService
		_switchDiscoveryService =
			new();

	private readonly AssistantSettingsService
		_settingsService =
			new();

	public PlayerProfilesWindow()
	{
		InitializeComponent();
	}

	private void Window_Loaded(
		object sender,
		RoutedEventArgs e
	)
	{
		RefreshLogicalProfiles();
		RefreshEdenProfiles();
	}

	private void CreateProfile_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		try
		{
			_linkService.CreateProfile(
				NewProfileNameTextBox.Text
			);

			NewProfileNameTextBox.Clear();

			RefreshLogicalProfiles();
		}
		catch (Exception ex)
		{
			ShowWarning(
				ex.Message
			);
		}
	}

	private void DeleteProfile_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (LogicalProfilesGrid.SelectedItem
			is not LogicalProfileRow row)
		{
			ShowWarning(
				LocalizationService.Get(
					"Profiles_SelectLogical"
				)
			);

			return;
		}

		if (MessageBox.Show(
				this,
				LocalizationService.Format(
					"Profiles_ConfirmDelete",
					row.Name
				),
				LocalizationService.Get(
					"Profiles_Title"
				),
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning
			) !=
			MessageBoxResult.Yes)
		{
			return;
		}

		_linkService.DeleteProfile(
			row.Id
		);

		RefreshLogicalProfiles();
	}

	private void LinkEden_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (!TryGetSelectedLogical(
			out var logical
		))
		{
			return;
		}

		if (EdenProfilesGrid.SelectedItem
			is not EdenProfileRow eden)
		{
			ShowWarning(
				LocalizationService.Get(
					"Profiles_SelectEden"
				)
			);

			return;
		}

		if (eden.IsDevice)
		{
			ShowWarning(
				LocalizationService.Get(
					"Profiles_DeviceCannotLink"
				)
			);

			return;
		}

		try
		{
			_linkService.Bind(
				logical.Id,
				new PlayerProfileBinding
					{
						Platform =
							"Eden",

						NativeId =
							eden.NativeId,

						DisplayName =
							eden.Name,

						IdentityKind =
							ProfileIdentityKind.StableId
					}
			);

			RefreshLogicalProfiles();

			ReconcileFolderAliasesAfterLink(
				logical.Id
			);
		}
		catch (Exception ex)
		{
			ShowWarning(
				ex.Message
			);
		}
	}

	private void LinkSwitch_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (!TryGetSelectedLogical(
			out var logical
		))
		{
			return;
		}

		if (SwitchProfilesGrid.SelectedItem
			is not SwitchProfileRow switchProfile)
		{
			ShowWarning(
				LocalizationService.Get(
					"Profiles_SelectSwitch"
				)
			);

			return;
		}

		try
		{
			_linkService.Bind(
				logical.Id,
				new PlayerProfileBinding
					{
						Platform =
							"Switch",

						NativeId =
							switchProfile.Name,

						DisplayName =
							switchProfile.Name,

						IdentityKind =
							ProfileIdentityKind.DisplayNameOnly
					}
			);

			RefreshLogicalProfiles();

			ReconcileFolderAliasesAfterLink(
				logical.Id
			);
		}
		catch (Exception ex)
		{
			ShowWarning(
				ex.Message
			);
		}
	}

	private void UnlinkEden_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		UnlinkSelected(
			"Eden"
		);
	}

	private void UnlinkSwitch_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		UnlinkSelected(
			"Switch"
		);
	}

	private void UnlinkSelected(
		string platform
	)
	{
		if (!TryGetSelectedLogical(
			out var logical
		))
		{
			return;
		}

		try
		{
			_linkService.Unbind(
				logical.Id,
				platform
			);

			RefreshLogicalProfiles();
		}
		catch (Exception ex)
		{
			ShowWarning(
				ex.Message
			);
		}
	}

	private void RefreshEden_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		RefreshEdenProfiles();
	}

	private void RefreshEdenProfiles()
	{
		try
		{
			var settings =
				_settingsService.Load();

			if (string.IsNullOrWhiteSpace(
				settings.EdenRootPath
			))
			{
				EdenProfilesGrid.ItemsSource =
					null;

				EdenStatusText.Text =
					LocalizationService.Get(
						"Profiles_EdenRootMissing"
					);

				return;
			}

			var result =
				_edenDiscoveryService.Discover(
					settings.EdenRootPath
				);

			var rows =
				result.Profiles
					.Select(
						profile =>
							new EdenProfileRow(
								profile.NativeId,
								profile.IsDevice
									? LocalizationService.Get(
										"Common_DeviceShared"
									)
									: profile.RegisteredName ??
										LocalizationService.Get(
											"Profiles_Unregistered"
										),
								profile.IsDevice
									? LocalizationService.Get(
										"Profiles_StatusDevice"
									)
									: profile.IsRegistered
										? LocalizationService.Get(
											"Profiles_StatusRegistered"
										)
										: LocalizationService.Get(
											"Profiles_StatusOrphan"
										),
								profile.GameCount,
								profile.IsDevice
							)
					)
					.ToArray();

			EdenProfilesGrid.ItemsSource =
				rows;

			EdenStatusText.Text =
				LocalizationService.Format(
					"Profiles_EdenSummary",
					rows.Count(
						row =>
							!row.IsDevice
					),
					rows.Count(
						row =>
							row.Status ==
							LocalizationService.Get(
								"Profiles_StatusOrphan"
							)
					)
				);
		}
		catch (Exception ex)
		{
			EdenProfilesGrid.ItemsSource =
				null;

			EdenStatusText.Text =
				ex.Message;
		}
	}

	private void DetectSwitch_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		try
		{
			SwitchStatusText.Text =
				LocalizationService.Get(
					"Profiles_SwitchScanning"
				);

			using var dbi =
				new DbiMtpClient();

			dbi.Connect();

			var profiles =
				_switchDiscoveryService.Discover(
					dbi
				);

			var rows =
				profiles
					.Select(
						profile =>
							new SwitchProfileRow(
								profile.EntryName,
								LocalizationService.Get(
									"Profiles_IdentityNameOnly"
								),
								profile.GameCount
							)
					)
					.ToArray();

			SwitchProfilesGrid.ItemsSource =
				rows;

			SwitchStatusText.Text =
				LocalizationService.Format(
					"Profiles_SwitchSummary",
					rows.Length
				);
		}
		catch (Exception ex)
		{
			SwitchProfilesGrid.ItemsSource =
				null;

			SwitchStatusText.Text =
				LocalizationService.Format(
					"Profiles_SwitchDetectionFailed",
					ex.Message
				);
		}
	}

	private void ReconcileFolderAliasesAfterLink(
		string logicalPlayerId
	)
	{
		try
		{
			var settings =
				_settingsService.Load();

			if (
				string.IsNullOrWhiteSpace(
					settings.FolderPath
				) ||
				!Directory.Exists(
					settings.FolderPath
				)
			)
			{
				return;
			}

			var document =
				_linkService.Load();

			var logical =
				document.Profiles.FirstOrDefault(
					profile =>
						string.Equals(
							profile.Id,
							logicalPlayerId,
							StringComparison.OrdinalIgnoreCase
						)
				);

			if (logical is null)
			{
				return;
			}

			var resolver =
				new PlayerProfileResolver(
					document
				);

			var repairService =
				new ProfileAliasRepairService();

			foreach (var category
				in new[]
					{
						"Installed games",
						"Uninstalled games"
					})
			{
				var categoryPath =
					Path.Combine(
						settings.FolderPath,
						category
					);

				if (!Directory.Exists(
					categoryPath
				))
				{
					continue;
				}

				foreach (var gamePath
					in Directory.EnumerateDirectories(
						categoryPath
					)
					.OrderBy(
						path =>
							Path.GetFileName(
								path
							),
						StringComparer.CurrentCultureIgnoreCase
					))
				{
					var aliases =
						Directory.EnumerateDirectories(
							gamePath
						)
							.Select(
								Path.GetFileName
							)
							.Where(
								name =>
									!string.IsNullOrWhiteSpace(
										name
									)
							)
							.Select(
								name =>
									name!
							)
							.Where(
								name =>
									{
										var resolution =
											resolver.ResolveFolderAlias(
												name
											);

										return
											resolution.Kind ==
												ProfileAliasResolutionKind.Linked &&
											string.Equals(
												resolution.LogicalPlayerId,
												logical.Id,
												StringComparison.OrdinalIgnoreCase
											);
									}
							)
							.Distinct(
								StringComparer.OrdinalIgnoreCase
							)
							.OrderBy(
								name =>
									name,
								StringComparer.CurrentCultureIgnoreCase
							)
							.ToArray();

					if (aliases.Length <
						2)
					{
						continue;
					}

					var gameName =
						Path.GetFileName(
							gamePath
						);

					if (string.IsNullOrWhiteSpace(
						gameName
					))
					{
						continue;
					}

					var plan =
						repairService.BuildPlan(
							settings.FolderPath,
							category,
							gameName,
							logical.DisplayName,
							aliases
						);

					if (plan.AllIdentical)
					{
						var selectedAlias =
							plan.Candidates.FirstOrDefault(
								candidate =>
									string.Equals(
										candidate.Alias,
										logical.DisplayName,
										StringComparison.OrdinalIgnoreCase
									)
							)?.Alias ??
							plan.Candidates[0].Alias;

						repairService.Execute(
							plan,
							selectedAlias
						);

						continue;
					}

					var window =
						new ProfileAliasRepairWindow(
							settings.FolderPath,
							category,
							gameName,
							logical.DisplayName,
							aliases
						)
							{
								Owner =
									this
							};

					window.ShowDialog();
				}
			}
		}
		catch (Exception ex)
		{
			ShowWarning(
				ex.Message
			);
		}
	}

	private void RefreshLogicalProfiles()
	{
		var rows =
			_linkService.Load()
				.Profiles
				.OrderBy(
					profile =>
						profile.DisplayName,
					StringComparer.CurrentCultureIgnoreCase
				)
				.Select(
					profile =>
						{
							var eden =
								profile.Bindings.FirstOrDefault(
									binding =>
										string.Equals(
											binding.Platform,
											"Eden",
											StringComparison.OrdinalIgnoreCase
										)
								);

							var switchBinding =
								profile.Bindings.FirstOrDefault(
									binding =>
										string.Equals(
											binding.Platform,
											"Switch",
											StringComparison.OrdinalIgnoreCase
										)
								);

							return new LogicalProfileRow(
								profile.Id,
								profile.DisplayName,
								FormatBinding(
									eden
								),
								FormatBinding(
									switchBinding
								)
							);
						}
				)
				.ToArray();

		LogicalProfilesGrid.ItemsSource =
			rows;
	}

	private static string FormatBinding(
		PlayerProfileBinding? binding
	)
	{
		if (binding is null)
		{
			return "—";
		}

		if (
			binding.IdentityKind ==
				ProfileIdentityKind.StableId &&
			binding.NativeId.Length >
				8
		)
		{
			return
				$"{binding.DisplayName} • {binding.NativeId[..8]}…";
		}

		return binding.DisplayName;
	}

	private bool TryGetSelectedLogical(
		out LogicalProfileRow logical
	)
	{
		if (LogicalProfilesGrid.SelectedItem
			is LogicalProfileRow selected)
		{
			logical =
				selected;

			return true;
		}

		ShowWarning(
			LocalizationService.Get(
				"Profiles_SelectLogical"
			)
		);

		logical =
			null!;

		return false;
	}

	private void ShowWarning(
		string message
	)
	{
		MessageBox.Show(
			this,
			message,
			LocalizationService.Get(
				"Profiles_Title"
			),
			MessageBoxButton.OK,
			MessageBoxImage.Information
		);
	}

	private sealed record LogicalProfileRow(
		string Id,
		string Name,
		string Eden,
		string Switch
	);

	private sealed record EdenProfileRow(
		string NativeId,
		string Name,
		string Status,
		int Games,
		bool IsDevice
	);

	private sealed record SwitchProfileRow(
		string Name,
		string Identity,
		int Games
	);
}

