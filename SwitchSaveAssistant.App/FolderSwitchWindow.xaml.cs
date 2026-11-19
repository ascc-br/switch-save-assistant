using SwitchSaveAssistant.App.Localization;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SwitchSaveAssistant.Core.Mtp;
using SwitchSaveAssistant.Core.Settings;
using SwitchSaveAssistant.Core.Sync;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Profiles;

namespace SwitchSaveAssistant.App;

public partial class FolderSwitchWindow : Window
{
	private string? _folderPath;

	private List<CatalogEntry> _folderEntries = new();

	private List<CatalogEntry> _switchEntries = new();

	private bool _switchConnected;

	private readonly AssistantSettingsService
		_settingsService =
			new();

	private readonly PlayerProfileLinkService
		_profileLinkService =
			new();

	private bool
		_preferencesLoaded;

	private Dictionary<string, CatalogComparison>
		_comparisonResults =
			new(
				StringComparer.OrdinalIgnoreCase
			);

	public FolderSwitchWindow()
	{
		InitializeComponent();

		LoadSavedPreferences();
	}
	
	private void RepairAliasConflict_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		if (
			sender is not Button button ||
			button.Tag is not CatalogEntry entry ||
			!entry.HasAliasConflict
		)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(
			_folderPath
		))
		{
			MessageBox.Show(
				this,
				LocalizationService.Get(
					"Switch_SelectFolderFirst"
				),
				"Switch Save Assistant",
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);

			return;
		}

		if (
			string.IsNullOrWhiteSpace(
				entry.LogicalPlayerName
			) ||
			string.IsNullOrWhiteSpace(
				entry.ConflictAliases
			)
		)
		{
			MessageBox.Show(
				this,
				LocalizationService.Get(
					"AliasRepair_InvalidConflict"
				),
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		var aliases =
			entry.ConflictAliases
				.Split(
					',',
					StringSplitOptions.TrimEntries |
					StringSplitOptions.RemoveEmptyEntries
				)
				.Distinct(
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		if (aliases.Length <
			2)
		{
			MessageBox.Show(
				this,
				LocalizationService.Get(
					"AliasRepair_InvalidConflict"
				),
				LocalizationService.Get(
					"AliasRepair_Title"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		var window =
			new ProfileAliasRepairWindow(
				_folderPath,
				entry.Category,
				entry.GameName,
				entry.LogicalPlayerName,
				aliases
			)
				{
					Owner =
						this
				};

		if (window.ShowDialog() !=
			true)
		{
			return;
		}

		try
		{
			_folderEntries =
				ResolveFolderEntries(
					ReadFolderCatalog()
				);

			_comparisonResults.Clear();

			RenderCatalog();

			if (_switchConnected)
			{
				ComparisonProgressText.Text =
					LocalizationService.Get(
						"AliasRepair_VerifyAgain"
					);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get(
					"Common_CannotReadFolderTitle"
				),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);
		}
	}

	private void OpenSideBySide_Click(
	object sender,
	RoutedEventArgs e
)
{
	if (sender is not Button button ||
		button.Tag is not CatalogEntry entry)
	{
		return;
	}

	if (entry.HasAliasConflict)
	{
		MessageBox.Show(
			this,
			LocalizationService.Get(
				"Switch_ProfileAliasConflictOpenBlocked"
			),
			"Switch Save Assistant",
			MessageBoxButton.OK,
			MessageBoxImage.Warning
		);

		return;
	}

	if (string.IsNullOrWhiteSpace(
		_folderPath
	))
	{
		MessageBox.Show(
			this,
			LocalizationService.Get("Switch_SelectFolderFirst"),
			"Switch Save Assistant",
			MessageBoxButton.OK,
			MessageBoxImage.Information
		);

		return;
	}

	if (!_switchConnected)
	{
		MessageBox.Show(
			this,
			LocalizationService.Get("Switch_DetectFirst"),
			"Switch Save Assistant",
			MessageBoxButton.OK,
			MessageBoxImage.Information
		);

		return;
	}

	try
	{
		var localCategoryPath =
			Path.Combine(
				_folderPath,
				entry.Category
			);

		var localGamePath =
			Path.Combine(
				localCategoryPath,
				entry.GameName
			);

		/*
		 * Se o jogo existe nos dois lados, abrimos
		 * diretamente as duas pastas do jogo.
		 *
		 * Assim o usuário arrasta apenas o perfil/save.
		 */
		var folderGameExists =
			Directory.Exists(
				localGamePath
			);

		var switchGameExists =
			_switchEntries.Any(
				item =>
					string.Equals(
						item.Category,
						entry.Category,
						StringComparison.OrdinalIgnoreCase
					) &&
					string.Equals(
						item.GameName,
						entry.GameName,
						StringComparison.OrdinalIgnoreCase
					)
			);

		string localExplorerPath;

		string[] remoteSegments;

		if (folderGameExists &&
			switchGameExists)
		{
			localExplorerPath =
				localGamePath;

			remoteSegments =
			[
				entry.Category,
				entry.GameName
			];
		}
		else
		{
			/*
			 * O jogo não existe em um dos lados.
			 *
			 * Abrimos o nível da categoria para que
			 * a pasta inteira do jogo possa ser
			 * arrastada manualmente.
			 */
			Directory.CreateDirectory(
				localCategoryPath
			);

			localExplorerPath =
				localCategoryPath;

			remoteSegments =
			[
				entry.Category
			];
		}

		new ExplorerPairService()
			.OpenSideBySide(
				this,
				localExplorerPath,
				"Switch",
				"7: Saves",
				remoteSegments
			);
	}
	catch (Exception ex)
	{
		MessageBox.Show(
			this,
			ex.Message,
			LocalizationService.Get("Common_CannotOpenExplorersTitle"),
			MessageBoxButton.OK,
			MessageBoxImage.Warning
		);
	}
}

	private void SelectFolder_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var dialog = new OpenFolderDialog
		{
			Title = LocalizationService.Get("Common_SelectCentralFolderDialog"),
			Multiselect = false
		};

		if (dialog.ShowDialog(this) != true)
		{
			return;
		}

		_folderPath = dialog.FolderName;

		FolderPathTextBox.Text =
			_folderPath;

		SavePreferences();

		try
		{
			_folderEntries =
				ResolveFolderEntries(ReadFolderCatalog());

			RenderCatalog();
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get("Common_CannotReadFolderTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);
		}
	}
	private async void DetectSwitch_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var wasConnected =
			_switchConnected;

		SwitchActionButton.IsEnabled =
			false;

		SwitchActionButton.Content =
			wasConnected
				? LocalizationService.Get("Common_Verifying")
				: LocalizationService.Get("Switch_Detecting");

		ComparisonProgressPanel.Visibility =
			Visibility.Visible;

		ComparisonProgressBar.IsIndeterminate =
			true;

		ComparisonProgressText.Text =
			LocalizationService.Get("Switch_PreparingCheck");

		List<CatalogEntry> folderEntries;

		try
		{
			folderEntries =
				ResolveFolderEntries(ReadFolderCatalog());
		}
		catch (Exception ex)
		{
			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get("Common_CannotReadFolderTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			SwitchActionButton.Content =
				wasConnected
					? LocalizationService.Get("Common_VerifyAgain")
					: LocalizationService.Get("Common_Detect");

			SwitchActionButton.IsEnabled =
				true;

			ComparisonProgressPanel.Visibility =
				Visibility.Collapsed;

			return;
		}

		IProgress<ComparisonProgress> progress =
			new Progress<ComparisonProgress>(
				update =>
				{
					ComparisonProgressBar.IsIndeterminate =
						false;

					ComparisonProgressBar.Maximum =
						Math.Max(
							1,
							update.Total
						);

					ComparisonProgressBar.Value =
						Math.Min(
							update.Current,
							Math.Max(
								1,
								update.Total
							)
						);

					ComparisonProgressText.Text =
						LocalizationService.TranslateRuntimeMessage(update.Label);

					SwitchActionButton.Content =
						update.Total > 0
							? LocalizationService.Format("Switch_ComparingCount", update.Current, update.Total)
							: LocalizationService.Get("Common_Verifying");
				}
			);

		SwitchStatusText.Text =
			LocalizationService.Get("Switch_SearchingDbi");

		try
		{
			var detection =
				await Task.Run(
					() =>
					{
						using var dbi =
							new DbiMtpClient();

						dbi.Connect();

						var switchEntries =
							ResolveSwitchEntries(
								ReadSwitchCatalog(
									dbi
								)
							);

						var comparisons =
							CompareSharedEntries(
								dbi,
								folderEntries,
								switchEntries,
								update =>
									progress.Report(
										update
									)
							);

						return new DetectionResult(
							dbi.DeviceDescription,
							switchEntries,
							comparisons
						);
					}
				);

			_folderEntries =
				folderEntries;

			_switchEntries =
				detection.SwitchEntries;

			_comparisonResults =
				detection.Comparisons;

			_switchConnected =
				true;

			SwitchStatusText.Text =
				LocalizationService.Format("Switch_Connected", detection.DeviceDescription);

			SwitchActionButton.Content =
				LocalizationService.Get("Common_VerifyAgain");

			RenderCatalog();
		}
		catch (Exception ex)
		{
			_switchConnected =
				false;

			_switchEntries.Clear();

			_comparisonResults.Clear();

			SwitchStatusText.Text =
				LocalizationService.Get("Switch_DbiNotFound");

			SwitchActionButton.Content =
				LocalizationService.Get("Common_Detect");

			RenderCatalog();

			MessageBox.Show(
				this,
				ex.Message,
				LocalizationService.Get("Switch_NotFoundTitle"),
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);
		}
		finally
		{
			SwitchActionButton.IsEnabled =
				true;

			ComparisonProgressPanel.Visibility =
				Visibility.Collapsed;

			ComparisonProgressBar.IsIndeterminate =
				false;
		}
	}

	private Dictionary<string, CatalogComparison>
		CompareSharedEntries(
			DbiMtpClient dbi,
			IReadOnlyList<CatalogEntry> folderEntries,
			IReadOnlyList<CatalogEntry> switchEntries,
			Action<ComparisonProgress>? progress
		)
	{
		var result =
			new Dictionary<string, CatalogComparison>(
				StringComparer.OrdinalIgnoreCase
			);

		if (string.IsNullOrWhiteSpace(
			_folderPath
		))
		{
			return result;
		}

		var switchByKey =
			switchEntries
				.Where(
					entry =>
						!entry.HasAliasConflict
				)
				.GroupBy(
					CreateKey,
					StringComparer.OrdinalIgnoreCase
				)
				.ToDictionary(
					group =>
						group.Key,
					group =>
						group.First(),
					StringComparer.OrdinalIgnoreCase
				);

		var shared =
			folderEntries
				.Where(
					entry =>
						!entry.HasAliasConflict &&
						switchByKey.ContainsKey(
							CreateKey(
								entry
							)
						)
				)
				.OrderBy(
					entry =>
						entry.GameName,
					StringComparer.OrdinalIgnoreCase
				)
				.ThenBy(
					entry =>
						GetEntryDisplayName(
							entry
						),
					StringComparer.OrdinalIgnoreCase
				)
				.ToArray();

		var localSnapshotService =
			new LocalSnapshotService();

		var switchSnapshotService =
			new SwitchSnapshotService();

		var comparisonService =
			new ManifestComparisonService();

		for (var index = 0;
			index < shared.Length;
			index++)
		{
			var entry =
				shared[index];

			var key =
				CreateKey(
					entry
				);

			var switchEntry =
				switchByKey[key];

			var current =
				index + 1;

			progress?.Invoke(
				new ComparisonProgress(
					current,
					shared.Length,
					$"{entry.GameName} • {GetEntryDisplayName(entry)}"
				)
			);

			try
			{
				var localPath =
					Path.Combine(
						_folderPath,
						entry.Category,
						entry.GameName,
						GetFolderEntryName(
							entry
						)
					);

				var remoteSave =
					dbi.FindSave(
						entry.Category,
						entry.GameName,
						GetSwitchEntryName(
							switchEntry
						)
					);

				var localSnapshot =
					localSnapshotService.Create(
						localPath
					);

				var remoteSnapshot =
					switchSnapshotService.Create(
						dbi,
						remoteSave
					);

				var comparison =
					comparisonService.Compare(
						localSnapshot,
						remoteSnapshot
					);

				if (comparison.AreEqual)
				{
					result[key] =
						new CatalogComparison(
							CatalogComparisonStatus.Synchronized,
							$"{localSnapshot.Count} arquivo(s) com SHA-256 idêntico."
						);

					continue;
				}

				var changedPaths =
					comparison.Changes
						.Take(
							3
						)
						.Select(
							change =>
								change.RelativePath
						)
						.ToArray();

				var suffix =
					comparison.Changes.Count >
					changedPaths.Length
						? $" (+{comparison.Changes.Count - changedPaths.Length})"
						: string.Empty;

				result[key] =
					new CatalogComparison(
						CatalogComparisonStatus.Divergent,
						$"{comparison.Changes.Count} diferença(s): " +
						string.Join(
							", ",
							changedPaths
						) +
						suffix
					);
			}
			catch (Exception ex)
			{
				result[key] =
					new CatalogComparison(
						CatalogComparisonStatus.Error,
						ex.Message
					);
			}
		}

		if (shared.Length == 0)
		{
			progress?.Invoke(
				new ComparisonProgress(
					0,
					0,
					LocalizationService.Get("Switch_NoneOnBoth")
				)
			);
		}

		return result;
	}



	private static string GetLocalizedHelpFileName()
	{
		return LocalizationService.CurrentLanguage switch
			{
				"es-ES" =>
					"conectar-switch.es-ES.html",

				"pt-BR" =>
					"conectar-switch.html",

				_ =>
					"connect-switch.en-US.html"
			};
	}
	private void OpenSwitchHelp_Click(
		object sender,
		RoutedEventArgs e
	)
	{
		var helpPath =
			Path.Combine(
				AppContext.BaseDirectory,
				"Help",
				GetLocalizedHelpFileName()
			);

		if (!File.Exists(helpPath))
		{
			MessageBox.Show(
				this,
				LocalizationService.Format("Switch_HelpNotFound", helpPath),
				"Switch Save Assistant",
				MessageBoxButton.OK,
				MessageBoxImage.Warning
			);

			return;
		}

		Process.Start(
			new ProcessStartInfo
			{
				FileName = helpPath,
				UseShellExecute = true
			}
		);
	}

	private static List<CatalogEntry>
		ReadSwitchCatalog(
			DbiMtpClient dbi
		)
	{
		var result =
			new List<CatalogEntry>();

		foreach (var category in Categories)
		{
			var games =
				dbi.GetGames(
					category
				);

			foreach (var game in games)
			{
				var saves =
					dbi.GetSaveEntries(
						game
					);

				foreach (var save in saves)
				{
					result.Add(
						new CatalogEntry(
							category,
							game.Name,
							save.EntryName,
							save.EntryType.ToString(),
							InFolder: false,
							InSwitch: true
						)
					);
				}
			}
		}

		return result;
	}

	private List<CatalogEntry>
		ReadFolderCatalog()
	{
		var result =
			new List<CatalogEntry>();

		if (string.IsNullOrWhiteSpace(
				_folderPath
			))
		{
			return result;
		}

		foreach (var category in Categories)
		{
			var categoryPath =
				Path.Combine(
					_folderPath,
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
				))
			{
				var gameName =
					Path.GetFileName(
						gamePath
					);

				foreach (var entryPath
					in Directory.EnumerateDirectories(
						gamePath
					))
				{
					var entryName =
						Path.GetFileName(
							entryPath
						);

					result.Add(
						new CatalogEntry(
							category,
							gameName,
							entryName,
							InferEntryType(
								entryName
							),
							InFolder: true,
							InSwitch: false
						)
					);
				}
			}
		}

		return result;
	}
	private void RenderCatalog()
	{
		GamesPanel.Children.Clear();

		var allEntries =
			MergeResolvedCatalogs();

		var folderCount =
			allEntries.Count(
				entry =>
					entry.InFolder
			);

		var switchCount =
			allEntries.Count(
				entry =>
					entry.InSwitch
			);

		var synchronizedCount =
			allEntries.Count(
				entry =>
					GetComparisonStatus(
						entry
					) ==
					CatalogComparisonStatus.Synchronized
			);

		var divergentCount =
			allEntries.Count(
				entry =>
					GetComparisonStatus(
						entry
					) ==
					CatalogComparisonStatus.Divergent
			);

		var errorCount =
			allEntries.Count(
				entry =>
					GetComparisonStatus(
						entry
					) ==
					CatalogComparisonStatus.Error
			);

		if (allEntries.Count == 0)
		{
			CatalogSummaryText.Text =
				LocalizationService.Get("Switch_NoSavesFound");

			GamesPanel.Children.Add(
				new TextBlock
				{
					Text =
						LocalizationService.Get("Switch_NoSavesEither"),

					Foreground =
						FindBrush(
							"TextFillColorSecondaryBrush",
							Brushes.Gray
						)
				}
			);

			return;
		}

		CatalogSummaryText.Text =
			LocalizationService.Format("Switch_SummaryTotal", allEntries.Count) +
			LocalizationService.Format("Switch_SummaryFolder", folderCount) +
			LocalizationService.Format("Switch_SummarySwitch", switchCount) +
			LocalizationService.Format("Switch_SummarySynchronized", synchronizedCount) +
			LocalizationService.Format("Switch_SummaryDivergent", divergentCount) +
			(
				errorCount > 0
					? LocalizationService.Format("Switch_SummaryErrors", errorCount)
					: string.Empty
			);

		var merged =
			ApplyCatalogFilter(
				allEntries
			);

		if (merged.Count == 0)
		{
			GamesPanel.Children.Add(
				new TextBlock
				{
					Text =
						LocalizationService.Get("Common_NoSavesForFilter"),

					Foreground =
						FindBrush(
							"TextFillColorSecondaryBrush",
							Brushes.Gray
						),

					Margin =
						new Thickness(
							4,
							8,
							0,
							0
						)
				}
			);

			return;
		}

		foreach (var categoryGroup
			in merged
				.GroupBy(
					entry =>
						entry.Category
				)
				.OrderBy(
					group =>
						CategoryOrder(
							group.Key
						)
				))
		{
			GamesPanel.Children.Add(
				new TextBlock
				{
					Text =
						categoryGroup.Key ==
						"Installed games"
							? LocalizationService.Get("Switch_InstalledGames")
							: LocalizationService.Get("Switch_UninstalledGames"),

					FontSize = 15,
					FontWeight =
						FontWeights.SemiBold,

					Margin =
						new Thickness(
							0,
							10,
							0,
							10
						)
				}
			);

			foreach (var gameGroup
				in categoryGroup
					.GroupBy(
						entry =>
							entry.GameName
					)
					.OrderBy(
						group =>
							group.Key,
						StringComparer.OrdinalIgnoreCase
					))
			{
				var gamePanel =
					new StackPanel
					{
						Margin =
							new Thickness(
								8,
								7,
								8,
								10
							)
					};

				gamePanel.Children.Add(
					new TextBlock
					{
						Text =
							$"🎮 {gameGroup.Key}",

						FontSize = 14,
						FontWeight =
							FontWeights.SemiBold
					}
				);

				foreach (var entry
					in gameGroup
						.OrderBy(
							item =>
								GetEntryDisplayName(
									item
								),
							StringComparer.OrdinalIgnoreCase
						))
				{
					var row =
						new Grid
						{
							Margin =
								new Thickness(
									10,
									5,
									0,
									0
								)
						};

					row.ColumnDefinitions.Add(
						new ColumnDefinition
						{
							Width =
								new GridLength(
									1,
									GridUnitType.Star
								)
						}
					);

					row.ColumnDefinitions.Add(
						new ColumnDefinition
						{
							Width =
								new GridLength(
									185
								)
						}
					);

					row.ColumnDefinitions.Add(
						new ColumnDefinition
						{
							Width =
								new GridLength(
									150
								)
						}
					);

					row.Children.Add(
						new TextBlock
						{
							Text =
								$"👤 {GetEntryDisplayName(entry)} " +
								$"[{entry.EntryType}]",

							ToolTip =
								GetEntryIdentityToolTip(
									entry
								),

							VerticalAlignment =
								VerticalAlignment.Center
						}
					);

					var statusText =
						new TextBlock
						{
							Text =
								GetPresenceText(
									entry
								),

							Foreground =
								GetPresenceForeground(
									entry
								),

							FontWeight =
								FontWeights.SemiBold,

							VerticalAlignment =
								VerticalAlignment.Center
						};

					var statusBadge =
						new Border
						{
							Background =
								GetPresenceBackground(
									entry
								),

							CornerRadius =
								new CornerRadius(
									999
								),

							Padding =
								new Thickness(
									10,
									4,
									10,
									4
								),

							HorizontalAlignment =
								HorizontalAlignment.Right,

							VerticalAlignment =
								VerticalAlignment.Center,

							ToolTip =
								GetPresenceDetail(
									entry
								),

							Child =
								statusText
						};

					Grid.SetColumn(
						statusBadge,
						1
					);

					row.Children.Add(
						statusBadge
					);

					var openButton =
						new Button
						{
							Content =
								entry.HasAliasConflict
									? LocalizationService.Get(
										"AliasRepair_Open"
									)
									: LocalizationService.Get(
										"Switch_OpenSideBySide"
									),

							Tag =
								entry,

							Width =
								135,

							Height =
								30,

							Margin =
								new Thickness(
									10,
									0,
									0,
									0
								),

							IsEnabled =
								!string.IsNullOrWhiteSpace(
									_folderPath
								) &&
								(
									entry.HasAliasConflict ||
									_switchConnected
								)
						};

					openButton.Click +=
						entry.HasAliasConflict
							? RepairAliasConflict_Click
							: OpenSideBySide_Click;

					Grid.SetColumn(
						openButton,
						2
					);

					row.Children.Add(
						openButton
					);

					gamePanel.Children.Add(
						row
					);
				}

				GamesPanel.Children.Add(
					gamePanel
				);
			}
		}
	}
	private void CatalogFilter_SelectionChanged(
		object sender,
		SelectionChangedEventArgs e
	)
	{
		if (!IsInitialized)
		{
			return;
		}

		RenderCatalog();

		SavePreferences();
	}

	private List<CatalogEntry> ApplyCatalogFilter(
		List<CatalogEntry> entries
	)
	{
		var selected =
			(CatalogFilterComboBox.SelectedItem
				as ComboBoxItem)
			?.Tag
			?.ToString()
			?? "All";

		return selected switch
		{
			"FolderOnly" =>
				entries
					.Where(
						entry =>
							entry.InFolder &&
							!entry.InSwitch
					)
					.ToList(),

			"SwitchOnly" =>
				entries
					.Where(
						entry =>
							entry.InSwitch &&
							!entry.InFolder
					)
					.ToList(),

			"Synchronized" =>
				entries
					.Where(
						entry =>
							GetComparisonStatus(
								entry
							) ==
							CatalogComparisonStatus.Synchronized
					)
					.ToList(),

			"Divergent" =>
				entries
					.Where(
						entry =>
							GetComparisonStatus(
								entry
							) ==
							CatalogComparisonStatus.Divergent
					)
					.ToList(),

			"Error" =>
				entries
					.Where(
						entry =>
							GetComparisonStatus(
								entry
							) ==
							CatalogComparisonStatus.Error
					)
					.ToList(),

			_ =>
				entries
		};
	}

	private CatalogComparisonStatus?
		GetComparisonStatus(
			CatalogEntry entry
		)
	{
		if (entry.HasAliasConflict)
		{
			return CatalogComparisonStatus.Error;
		}

		if (!entry.InFolder ||
			!entry.InSwitch)
		{
			return null;
		}

		return _comparisonResults.TryGetValue(
			CreateKey(
				entry
			),
			out var comparison
		)
			? comparison.Status
			: null;
	}

	private string GetPresenceDetail(
		CatalogEntry entry
	)
	{
		if (entry.HasAliasConflict)
		{
			return LocalizationService.Format(
				"Switch_ProfileAliasConflictDetail",
				entry.LogicalPlayerName ??
					entry.EntryName,
				entry.ConflictAliases ??
					entry.EntryName
			);
		}

		if (entry.InFolder &&
			entry.InSwitch &&
			_comparisonResults.TryGetValue(
				CreateKey(
					entry
				),
				out var comparison
			))
		{
			return comparison.Detail;
		}

		if (entry.InFolder &&
			entry.InSwitch)
		{
			return LocalizationService.Get("Switch_PresenceBothUncompared");
		}

		if (entry.InFolder)
		{
			return LocalizationService.Get("Switch_PresenceOnlyFolder");
		}

		return LocalizationService.Get("Switch_PresenceOnlySwitch");
	}

	private Brush GetPresenceBackground(
		CatalogEntry entry
	)
	{
		var status =
			GetComparisonStatus(
				entry
			);

		return status switch
		{
			CatalogComparisonStatus.Synchronized =>
				FindBrush(
					"SystemFillColorSuccessBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							36,
							22,
							163,
							74
						)
					)
				),

			CatalogComparisonStatus.Divergent =>
				FindBrush(
					"SystemFillColorCautionBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							42,
							217,
							119,
							6
						)
					)
				),

			CatalogComparisonStatus.Error =>
				FindBrush(
					"SystemFillColorCriticalBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							42,
							220,
							38,
							38
						)
					)
				),

			_ when entry.InFolder &&
				entry.InSwitch =>
				FindBrush(
					"SystemFillColorNeutralBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							32,
							128,
							128,
							128
						)
					)
				),

			_ when entry.InFolder =>
				FindBrush(
					"SystemFillColorAttentionBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							35,
							37,
							99,
							235
						)
					)
				),

			_ =>
				FindBrush(
					"SystemFillColorNeutralBackgroundBrush",
					new SolidColorBrush(
						Color.FromArgb(
							35,
							107,
							114,
							128
						)
					)
				)
		};
	}

	private Brush GetPresenceForeground(
		CatalogEntry entry
	)
	{
		var status =
			GetComparisonStatus(
				entry
			);

		return status switch
		{
			CatalogComparisonStatus.Synchronized =>
				FindBrush(
					"SystemFillColorSuccessBrush",
					Brushes.Green
				),

			CatalogComparisonStatus.Divergent =>
				FindBrush(
					"SystemFillColorCautionBrush",
					Brushes.DarkOrange
				),

			CatalogComparisonStatus.Error =>
				FindBrush(
					"SystemFillColorCriticalBrush",
					Brushes.Red
				),

			_ when entry.InFolder &&
				entry.InSwitch =>
				FindBrush(
					"TextFillColorPrimaryBrush",
					Brushes.Gray
				),

			_ when entry.InFolder =>
				FindBrush(
					"AccentTextFillColorPrimaryBrush",
					Brushes.DodgerBlue
				),

			_ =>
				FindBrush(
					"TextFillColorSecondaryBrush",
					Brushes.Gray
				)
		};
	}

	private static Brush FindBrush(
		string key,
		Brush fallback
	)
	{
		return Application.Current
			?.TryFindResource(
				key
			)
			as Brush
			?? fallback;
	}



	private void LoadSavedPreferences()
	{
		var settings =
			_settingsService.Load();

		if (!string.IsNullOrWhiteSpace(
				settings.FolderPath
			) &&
			Directory.Exists(
				settings.FolderPath
			))
		{
			_folderPath =
				settings.FolderPath;

			FolderPathTextBox.Text =
				_folderPath;

			try
			{
				_folderEntries =
					ResolveFolderEntries(ReadFolderCatalog());
			}
			catch
			{
				_folderEntries.Clear();
			}
		}

		SelectFilterByTag(
			settings.SwitchFilter
		);

		_preferencesLoaded =
			true;

		RenderCatalog();
	}

	private void SavePreferences()
	{
		if (!_preferencesLoaded)
		{
			return;
		}

		var filter =
			(CatalogFilterComboBox.SelectedItem
				as ComboBoxItem)
			?.Tag
			?.ToString()
			?? "All";

		_settingsService.Update(
			current =>
				current with
				{
					FolderPath =
						_folderPath,

					SwitchFilter =
						filter
				}
		);
	}

	private void SelectFilterByTag(
		string? tag
	)
	{
		foreach (var item
			in CatalogFilterComboBox.Items
				.OfType<ComboBoxItem>())
		{
			if (string.Equals(
				item.Tag?.ToString(),
				tag,
				StringComparison.OrdinalIgnoreCase
			))
			{
				CatalogFilterComboBox.SelectedItem =
					item;

				return;
			}
		}

		CatalogFilterComboBox.SelectedIndex =
			0;
	}
	private List<CatalogEntry> ResolveFolderEntries(
		List<CatalogEntry> entries
	)
	{
		var resolver =
			new PlayerProfileResolver(
				_profileLinkService.Load()
			);

		var resolved =
			entries
				.Select(
					entry =>
						ResolveCatalogEntry(
							entry,
							resolver.ResolveFolderAlias(
								entry.EntryName
							),
							folderSide: true
						)
				)
				.ToList();

		var conflictGroups =
			resolved
				.Where(
					entry =>
						string.Equals(
							entry.EntryType,
							"User",
							StringComparison.OrdinalIgnoreCase
						) &&
						!string.IsNullOrWhiteSpace(
							entry.LogicalPlayerId
						)
				)
				.GroupBy(
					CreateLogicalGameKey,
					StringComparer.OrdinalIgnoreCase
				)
				.Where(
					group =>
						group
							.Select(
								entry =>
									entry.EntryName
							)
							.Distinct(
								StringComparer.OrdinalIgnoreCase
							)
							.Count() >
						1
				)
				.ToDictionary(
					group =>
						group.Key,
					group =>
						string.Join(
							", ",
							group
								.Select(
									entry =>
										entry.EntryName
								)
								.Distinct(
									StringComparer.OrdinalIgnoreCase
								)
								.OrderBy(
									name =>
										name,
									StringComparer.CurrentCultureIgnoreCase
								)
						),
					StringComparer.OrdinalIgnoreCase
				);

		return resolved
			.Select(
				entry =>
					!string.IsNullOrWhiteSpace(
						entry.LogicalPlayerId
					) &&
					conflictGroups.TryGetValue(
						CreateLogicalGameKey(
							entry
						),
						out var aliases
					)
						? entry with
							{
								HasAliasConflict =
									true,

								ConflictAliases =
									aliases
							}
						: entry
			)
			.ToList();
	}

	private List<CatalogEntry> ResolveSwitchEntries(
		List<CatalogEntry> entries
	)
	{
		var resolver =
			new PlayerProfileResolver(
				_profileLinkService.Load()
			);

		return entries
			.Select(
				entry =>
					ResolveCatalogEntry(
						entry,
						resolver.ResolveSwitchName(
							entry.EntryName
						),
						folderSide: false
					)
			)
			.ToList();
	}

	private static CatalogEntry ResolveCatalogEntry(
		CatalogEntry entry,
		ProfileAliasResolution resolution,
		bool folderSide
	)
	{
		if (!string.Equals(
			entry.EntryType,
			"User",
			StringComparison.OrdinalIgnoreCase
		))
		{
			return entry with
				{
					FolderEntryName =
						folderSide
							? entry.EntryName
							: null,

					SwitchEntryName =
						folderSide
							? null
							: entry.EntryName
				};
		}

		return entry with
			{
				LogicalPlayerId =
					resolution.Kind ==
						ProfileAliasResolutionKind.Linked
						? resolution.LogicalPlayerId
						: null,

				LogicalPlayerName =
					resolution.Kind ==
						ProfileAliasResolutionKind.Linked
						? resolution.LogicalPlayerName
						: null,

				FolderEntryName =
					folderSide
						? entry.EntryName
						: null,

				SwitchEntryName =
					folderSide
						? null
						: entry.EntryName,

				HasAliasConflict =
					resolution.Kind ==
						ProfileAliasResolutionKind.Ambiguous,

				ConflictAliases =
					resolution.Kind ==
						ProfileAliasResolutionKind.Ambiguous
						? entry.EntryName
						: null
			};
	}

	private List<CatalogEntry>
		MergeResolvedCatalogs()
	{
		var merged =
			new Dictionary<string, CatalogEntry>(
				StringComparer.OrdinalIgnoreCase
			);

		var folderConflictGroups =
			_folderEntries
				.Where(
					entry =>
						entry.HasAliasConflict &&
						!string.IsNullOrWhiteSpace(
							entry.LogicalPlayerId
						)
				)
				.GroupBy(
					CreateLogicalGameKey,
					StringComparer.OrdinalIgnoreCase
				)
				.ToDictionary(
					group =>
						group.Key,
					group =>
						group.ToArray(),
					StringComparer.OrdinalIgnoreCase
				);

		foreach (var entry
			in _folderEntries.Where(
				entry =>
					entry.HasAliasConflict &&
					string.IsNullOrWhiteSpace(
						entry.LogicalPlayerId
					)
			))
		{
			merged[
				CreateKey(
					entry
				)
			] =
				entry;
		}

		foreach (var group
			in folderConflictGroups)
		{
			var first =
				group.Value[0];

			var aliases =
				first.ConflictAliases ??
				string.Join(
					", ",
					group.Value
						.Select(
							entry =>
								entry.EntryName
						)
						.Distinct(
							StringComparer.OrdinalIgnoreCase
						)
				);

			var synthetic =
				first with
					{
						EntryName =
							first.LogicalPlayerName ??
							first.EntryName,

						FolderEntryName =
							null,

						SwitchEntryName =
							null,

						InFolder =
							true,

						InSwitch =
							false,

						HasAliasConflict =
							true,

						ConflictAliases =
							aliases
					};

			merged[
				CreateKey(
					synthetic
				)
			] =
				synthetic;
		}

		foreach (var entry
			in _folderEntries.Where(
				entry =>
					!entry.HasAliasConflict
			))
		{
			merged[
				CreateKey(
					entry
				)
			] =
				entry;
		}

		foreach (var sourceEntry
			in _switchEntries)
		{
			var entry =
				sourceEntry;

			if (
				entry.HasAliasConflict &&
				string.IsNullOrWhiteSpace(
					entry.LogicalPlayerId
				)
			)
			{
				merged[
					CreateKey(
						entry
					)
				] =
					entry;

				continue;
			}

			if (
				!string.IsNullOrWhiteSpace(
					entry.LogicalPlayerId
				) &&
				folderConflictGroups.TryGetValue(
					CreateLogicalGameKey(
						entry
					),
					out var conflictGroup
				)
			)
			{
				var first =
					conflictGroup[0];

				var conflictKey =
					CreateConflictKey(
						first
					);

				if (merged.TryGetValue(
					conflictKey,
					out var conflict
				))
				{
					merged[conflictKey] =
						conflict with
							{
								InSwitch =
									true,

								SwitchEntryName =
									entry.EntryName
							};
				}

				continue;
			}

			var key =
				CreateKey(
					entry
				);

			if (merged.TryGetValue(
				key,
				out var existing
			))
			{
				merged[key] =
					existing with
						{
							Category =
								entry.Category,

							GameName =
								entry.GameName,

							EntryType =
								entry.EntryType,

							InSwitch =
								true,

							LogicalPlayerId =
								existing.LogicalPlayerId ??
								entry.LogicalPlayerId,

							LogicalPlayerName =
								existing.LogicalPlayerName ??
								entry.LogicalPlayerName,

							FolderEntryName =
								existing.FolderEntryName ??
								(
									existing.InFolder
										? existing.EntryName
										: null
								),

							SwitchEntryName =
								entry.SwitchEntryName ??
								entry.EntryName
						};
			}
			else
			{
				merged[key] =
					entry;
			}
		}

		return merged
			.Values
			.OrderBy(
				entry =>
					entry.Category,
				StringComparer.OrdinalIgnoreCase
			)
			.ThenBy(
				entry =>
					entry.GameName,
				StringComparer.CurrentCultureIgnoreCase
			)
			.ThenBy(
				entry =>
					GetEntryDisplayName(
						entry
					),
				StringComparer.CurrentCultureIgnoreCase
			)
			.ToList();
	}

	private static string CreateLogicalGameKey(
		CatalogEntry entry
	)
	{
		return string.Join(
			'\u001F',
			entry.Category,
			entry.GameName,
			entry.LogicalPlayerId ??
				string.Empty
		);
	}

	private static string CreateConflictKey(
		CatalogEntry entry
	)
	{
		var identity =
			entry.LogicalPlayerId ??
			entry.EntryName;

		var side =
			string.IsNullOrWhiteSpace(
				entry.LogicalPlayerId
			)
				? entry.InFolder
					? "folder"
					: "switch"
				: "logical";

		return string.Join(
			'\u001F',
			entry.Category,
			entry.GameName,
			"conflict",
			identity,
			side
		);
	}

	private static string GetEntryDisplayName(
		CatalogEntry entry
	)
	{
		return
			entry.LogicalPlayerName ??
			entry.EntryName;
	}

	private static string? GetEntryIdentityToolTip(
		CatalogEntry entry
	)
	{
		if (entry.HasAliasConflict)
		{
			return LocalizationService.Format(
				"Switch_ProfileAliasConflictDetail",
				entry.LogicalPlayerName ??
					entry.EntryName,
				entry.ConflictAliases ??
					entry.EntryName
			);
		}

		if (!string.IsNullOrWhiteSpace(
			entry.LogicalPlayerId
		))
		{
			return LocalizationService.Format(
				"Switch_ProfileLinkedDetail",
				entry.LogicalPlayerName ??
					entry.EntryName,
				entry.FolderEntryName ??
					"—",
				entry.SwitchEntryName ??
					"—"
			);
		}

		return null;
	}

	private static string GetFolderEntryName(
		CatalogEntry entry
	)
	{
		return
			entry.FolderEntryName ??
			entry.EntryName;
	}

	private static string GetSwitchEntryName(
		CatalogEntry entry
	)
	{
		return
			entry.SwitchEntryName ??
			entry.EntryName;
	}

	private string GetPresenceText(
		CatalogEntry entry
	)
	{
		if (entry.HasAliasConflict)
		{
			return LocalizationService.Get(
				"Switch_ProfileAliasConflict"
			);
		}

		var comparisonStatus =
			GetComparisonStatus(
				entry
			);

		if (comparisonStatus ==
			CatalogComparisonStatus.Synchronized)
		{
			return LocalizationService.Get("Switch_StateSynchronized");
		}

		if (comparisonStatus ==
			CatalogComparisonStatus.Divergent)
		{
			return LocalizationService.Get("Switch_StateDivergent");
		}

		if (comparisonStatus ==
			CatalogComparisonStatus.Error)
		{
			return LocalizationService.Get("Switch_StateError");
		}

		if (entry.InFolder &&
			entry.InSwitch)
		{
			return LocalizationService.Get("Switch_StateNotCompared");
		}

		if (entry.InFolder)
		{
			return LocalizationService.Get("Switch_StateOnlyFolder");
		}

		return LocalizationService.Get("Switch_StateOnlySwitch");
	}


	private static string InferEntryType(
		string entryName
	)
	{
		if (string.Equals(
			entryName,
			"Device",
			StringComparison.OrdinalIgnoreCase
		))
		{
			return "Device";
		}

		if (string.Equals(
			entryName,
			"BCAT",
			StringComparison.OrdinalIgnoreCase
		))
		{
			return "Bcat";
		}

		return "User";
	}

	private static int CategoryOrder(
		string category
	)
	{
		return string.Equals(
			category,
			"Installed games",
			StringComparison.OrdinalIgnoreCase
		)
			? 0
			: 1;
	}

	private static string CreateKey(
		CatalogEntry entry
	)
	{
		if (entry.HasAliasConflict)
		{
			return CreateConflictKey(
				entry
			);
		}

		if (
			string.Equals(
				entry.EntryType,
				"User",
				StringComparison.OrdinalIgnoreCase
			) &&
			!string.IsNullOrWhiteSpace(
				entry.LogicalPlayerId
			)
		)
		{
			return string.Join(
				'\u001F',
				entry.Category,
				entry.GameName,
				"logical",
				entry.LogicalPlayerId
			);
		}

		return string.Join(
			'\u001F',
			entry.Category,
			entry.GameName,
			entry.EntryName
		);
	}

	private static readonly string[]
		Categories =
		[
			"Installed games",
			"Uninstalled games"
		];

	private enum CatalogComparisonStatus
	{
		Synchronized,
		Divergent,
		Error
	}

	private sealed record CatalogComparison(
		CatalogComparisonStatus Status,
		string Detail
	);

	private sealed record ComparisonProgress(
		int Current,
		int Total,
		string Label
	);

	private sealed record DetectionResult(
		string DeviceDescription,
		List<CatalogEntry> SwitchEntries,
		Dictionary<string, CatalogComparison> Comparisons
	);
private sealed record CatalogEntry(
		string Category,
		string GameName,
		string EntryName,
		string EntryType,
		bool InFolder,
		bool InSwitch,
		string? LogicalPlayerId = null,
		string? LogicalPlayerName = null,
		string? FolderEntryName = null,
		string? SwitchEntryName = null,
		bool HasAliasConflict = false,
		string? ConflictAliases = null
	);
}














































