using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using MCLauncher.WPFDataTypes;
using Path = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;

namespace MCLauncher
{
    public partial class ModernMainWindow : Window, ICommonVersionCommands
    {
        // P/Invoke for 8.3 short path conversion
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);
        
        private static readonly string PREFS_PATH = @"preferences.json";
        private static readonly string IMPORTED_VERSIONS_PATH = @"imported_versions";
        private static readonly string VERSIONS_API_UWP = "https://mrarm.io/r/w10-vdb";
        private static readonly string VERSIONS_API_GDK = "https://raw.githubusercontent.com/MinecraftBedrockArchiver/GdkLinks/refs/heads/master/urls.min.json";
        private static readonly string RUNNING_VERSION_PATH = @"running_version.txt";

        private VersionList _versions;
        public Preferences UserPrefs { get; }

        private readonly VersionDownloader _anonVersionDownloader = new VersionDownloader();
        private readonly VersionDownloader _userVersionDownloader = new VersionDownloader();
        private readonly Task _userVersionDownloaderLoginTask;
        private volatile bool _hasLaunchTask = false;
        
        // Track currently running Minecraft version
        private WPFDataTypes.Version _runningVersion = null;

        private ObservableCollection<ModernVersionViewModel> _installedVersions;
        private ObservableCollection<ModernVersionViewModel> _availableVersions;
        private List<ModernVersionViewModel> _allVersions;
        private List<ModernVersionViewModel> _pagedFull;
        private int _pageStart;
        private const int PageSize = 30;

        // Download pause/resume tracking
        private Dictionary<WPFDataTypes.Version, CancellationTokenSource> _downloadCancelTokens = new Dictionary<WPFDataTypes.Version, CancellationTokenSource>();
        private Dictionary<WPFDataTypes.Version, bool> _downloadPausedState = new Dictionary<WPFDataTypes.Version, bool>();
        private readonly SemaphoreSlim _gdkExtractSemaphore = new SemaphoreSlim(1, 1); // ATOMIC: Only one GDK extraction at a time
        
        // Active downloads tracking
        private HashSet<WPFDataTypes.Version> _activeDownloads = new HashSet<WPFDataTypes.Version>();
        private object _downloadLock = new object();
        
        // CRITICAL: Protect concurrent extractions and file operations
        private readonly SemaphoreSlim _extractionSemaphore = new SemaphoreSlim(4, 4); // Allow 4 concurrent UWP extractions max
        private readonly object _prefsLock = new object(); // Protect preferences.json writes
        private Window _activeDownloadsDialog = null;
        private System.Windows.Threading.DispatcherTimer _downloadDialogUpdateTimer = null;

        // Auto-scroll support
        private bool _isAutoScrolling = false;
        private Point _autoScrollStartPoint;
        private ScrollViewer _autoScrollViewer;
        private DispatcherTimer _autoScrollTimer;
        private Ellipse _autoScrollIndicator;
        
        // Minecraft version monitoring (lightweight process check)
        private DispatcherTimer _versionMonitorTimer;
        private bool _wasMinecraftRunning = false;
        private string _cachedSystemVersion = null;
        private DateTime _lastSystemVersionCheck = DateTime.MinValue;

        public ModernMainWindow()
        {
            InitializeComponent();

            // Apply custom colors from preferences if set
            ApplyCustomColors();

            // Load embedded icon
            var icon = EmbeddedIcon.GetIcon();
            if (icon != null)
            {
                MyVersionsHeaderIcon.Source = icon;
            }

            // Load embedded avatar
            var avatar = EmbeddedAvatar.GetAvatar();
            if (avatar != null)
            {
                CreatorAvatar.Source = avatar;
            }

            // Load preferences
            if (File.Exists(PREFS_PATH))
            {
                UserPrefs = JsonConvert.DeserializeObject<Preferences>(File.ReadAllText(PREFS_PATH));
            }
            else
            {
                UserPrefs = new Preferences();
                RewritePrefs();
            }

            var versionsApiUWP = UserPrefs.VersionsApiUWP != "" ? UserPrefs.VersionsApiUWP : VERSIONS_API_UWP;
            var versionsApiGDK = UserPrefs.VersionsApiGDK != "" ? UserPrefs.VersionsApiGDK : VERSIONS_API_GDK;
            _versions = new VersionList("versions_uwp.json", IMPORTED_VERSIONS_PATH, versionsApiUWP, this, VersionEntryPropertyChanged, "versions_gdk.json", versionsApiGDK);

            _installedVersions = new ObservableCollection<ModernVersionViewModel>();
            _availableVersions = new ObservableCollection<ModernVersionViewModel>();
            _allVersions = new List<ModernVersionViewModel>();
            _pagedFull = new List<ModernVersionViewModel>();
            _pageStart = 0;

            InstalledVersionsList.ItemsSource = _installedVersions;
            AvailableVersionsList.ItemsSource = _availableVersions;

            _userVersionDownloaderLoginTask = new Task(() =>
            {
                _userVersionDownloader.EnableUserAuthorization();
            });

            // Initialize auto-scroll timer
            _autoScrollTimer = new DispatcherTimer();
            _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
            
            // Initialize lightweight Minecraft version monitor (process check only)
            _versionMonitorTimer = new DispatcherTimer();
            _versionMonitorTimer.Interval = TimeSpan.FromSeconds(3);
            _versionMonitorTimer.Tick += VersionMonitorTimer_Tick;
            _versionMonitorTimer.Start();

            Loaded += ModernMainWindow_Loaded;
            Closing += (s, e) => 
            {
                Debug.WriteLine("ModernMainWindow is closing");
                // Cleanup timers
                _versionMonitorTimer?.Stop();
                _autoScrollTimer?.Stop();
                _downloadDialogUpdateTimer?.Stop();
            };
            Closed += (s, e) => Debug.WriteLine("ModernMainWindow has closed");
            
            Debug.WriteLine("ModernMainWindow constructor completed");
        }
        

        private async void ModernMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("ModernMainWindow_Loaded started");
                
                // Load saved language preference
                SetLanguage(UserPrefs.Language ?? "en");
                Debug.WriteLine("Language set");
                
                // Check status indicators
                UpdateStatusIndicators();
                Debug.WriteLine("Status indicators updated");
                
                Debug.WriteLine("Starting LoadVersionList");
                await LoadVersionList();
                Debug.WriteLine("ModernMainWindow_Loaded completed successfully");
                
                // CRITICAL: Initial title update after loading
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in ModernMainWindow_Loaded: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show(
                    $"Failed to load main window:\n\n{ex.Message}\n\nCheck Log.txt for details.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
        private async Task LoadVersionList()
        {
            LoadingIndicator.Visibility = Visibility.Visible;
            AvailableVersionsList.Visibility = Visibility.Collapsed;

            _versions.PrepareForReload();

            LoadingText.Text = "Loading cached versions...";
            Debug.WriteLine("Loading cached versions...");
            try
            {
                await _versions.LoadFromCacheGDK();
                await _versions.LoadFromCacheUWP();
                Debug.WriteLine($"Loaded {_versions.Count} versions from cache");
            }
            catch (Exception e)
            {
                Debug.WriteLine("List cache load failed:\n" + e.ToString());
            }

            _versions.PrepareForReload();

            LoadingText.Text = "Downloading latest version list...";
            Debug.WriteLine("Downloading version lists...");
            try
            {
                await _versions.DownloadVersionsGDK();
                await _versions.DownloadVersionsUWP();
                Debug.WriteLine($"Downloaded versions, total count: {_versions.Count}");
            }
            catch (Exception e)
            {
                Debug.WriteLine("List download failed:\n" + e.ToString());
                ShowFriendlyError(Localization.Get("CouldntUpdateList"), 
                    Localization.Get("CouldntUpdateListMessage"));
            }

            LoadingText.Text = "Loading imported versions...";
            Debug.WriteLine("Loading imported versions...");
            await _versions.LoadImported();
            Debug.WriteLine($"Final version count: {_versions.Count}");

            LoadingIndicator.Visibility = Visibility.Collapsed;
            AvailableVersionsList.Visibility = Visibility.Visible;

            RefreshVersionLists();
            Debug.WriteLine($"Refreshed lists - Installed: {_installedVersions.Count}, Available: {_availableVersions.Count}");
            
            // CRITICAL: Force WPF to re-evaluate all command CanExecute predicates
            // Without this, buttons stay grayed out until user interaction
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshVersionLists()
        {
            _installedVersions.Clear();
            _allVersions.Clear();

            Debug.WriteLine($"RefreshVersionLists called - Total versions: {_versions.Count}");

            // Load registered version if not already loaded
            if (_runningVersion == null)
            {
                _runningVersion = LoadRunningVersion();
            }

            foreach (var version in _versions)
            {
                var viewModel = new ModernVersionViewModel(version, this);
                
                // Mark if this is the registered version
                viewModel.IsRegistered = (_runningVersion != null && version == _runningVersion);

                // Add to installed list if it's actually installed OR if it's an imported version (even if not yet extracted)
                if (version.IsInstalled || version.IsImported)
                {
                    _installedVersions.Add(viewModel);
                }

                _allVersions.Add(viewModel);
            }

            // Show empty state
            EmptyState.Visibility = _installedVersions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            _pagedFull = new List<ModernVersionViewModel>(_allVersions);
            _pageStart = 0;
            RenderPage();

            Debug.WriteLine($"Final counts - Installed: {_installedVersions.Count}, Available: {_availableVersions.Count}");
            
            // CRITICAL: Force command re-evaluation after list refresh
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void RenderPage()
        {
            if (_pagedFull == null)
                _pagedFull = new List<ModernVersionViewModel>();
            _availableVersions.Clear();
            int end = Math.Min(_pageStart + PageSize, _pagedFull.Count);
            for (int i = _pageStart; i < end; i++)
                _availableVersions.Add(_pagedFull[i]);
            UpdatePager();
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void UpdatePager()
        {
            if (BrowsePager == null)
                return;
            int total = _pagedFull == null ? 0 : _pagedFull.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            int currentPage = total == 0 ? 0 : (_pageStart / PageSize) + 1;
            BrowsePageText.Text = $"{currentPage} / {totalPages}";
            BrowsePrevButton.IsEnabled = _pageStart > 0;
            BrowseNextButton.IsEnabled = _pageStart + PageSize < total;
            BrowsePager.Visibility = total > PageSize ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowsePrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pageStart <= 0)
                return;
            _pageStart = Math.Max(0, _pageStart - PageSize);
            RenderPage();
            ScrollToTopOfBrowse();
        }

        private void BrowseNextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pagedFull == null || _pageStart + PageSize >= _pagedFull.Count)
                return;
            _pageStart = Math.Min(_pageStart + PageSize, Math.Max(0, _pagedFull.Count - PageSize));
            RenderPage();
            ScrollToTopOfBrowse();
        }

        private void ScrollToTopOfBrowse()
        {
            var sv = FindVisualChild<ScrollViewer>(AvailableVersionsList);
            if (sv != null)
            {
                sv.BeginAnimation(SmoothScrollOffsetProperty, null);
                sv.ScrollToVerticalOffset(0);
            }
        }

        private void VersionEntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() => {
                RefreshVersionLists();
                // CRITICAL: Update window title to show busy state for user feedback
                if (e.PropertyName == "IsStateChanging" || e.PropertyName == "StateChangeInfo") {
                    UpdateWindowTitle();
                }
                // CRITICAL: Force command re-evaluation on any property change
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            });
        }

        private void UpdateWindowTitle() {
            // CRITICAL: Visual feedback for users - show busy state in title
            var stagingVersion = _versions.FirstOrDefault(v => v.StateChangeInfo?.VersionState == VersionState.Staging);
            var unregisteringVersion = _versions.FirstOrDefault(v => v.StateChangeInfo?.VersionState == VersionState.Unregistering);
            var registeringVersion = _versions.FirstOrDefault(v => v.StateChangeInfo?.VersionState == VersionState.Registering);
            
            if (stagingVersion != null) {
                // Show staging prominently - it blocks all other operations
                this.Title = $"⚠️ STAGING IN PROGRESS - Please wait... (All operations disabled)";
            } else if (unregisteringVersion != null) {
                // Show unregistering prominently - it blocks all other operations
                this.Title = $"⚠️ UNREGISTERING PACKAGE - Please wait... (All operations disabled)";
            } else if (registeringVersion != null) {
                // Show registering prominently - it blocks all other operations
                this.Title = $"⚠️ REGISTERING PACKAGE - Please wait... (All operations disabled)";
            } else {
                int busyCount = _versions.Count(v => v.IsStateChanging);
                if (busyCount > 0) {
                    this.Title = $"⏳ Minecraft Bedrock Launcher - {busyCount} operation(s) in progress...";
                } else {
                    this.Title = "Minecraft Bedrock Launcher";
                }
            }
        }

        private System.Version ParseVersionNumber(string versionName)
        {
            try
            {
                // Try to parse version like "1.21.50.07" or "1.20.0.1"
                // Remove any non-numeric prefixes and suffixes
                var parts = versionName.Split('.');
                if (parts.Length >= 2)
                {
                    // Build a proper Version object (max 4 parts)
                    int major = 0, minor = 0, build = 0, revision = 0;
                    
                    if (parts.Length > 0 && int.TryParse(parts[0], out major) &&
                        parts.Length > 1 && int.TryParse(parts[1], out minor))
                    {
                        if (parts.Length > 2) int.TryParse(parts[2], out build);
                        if (parts.Length > 3) int.TryParse(parts[3], out revision);
                        
                        return new System.Version(major, minor, build, revision);
                    }
                }
            }
            catch
            {
                // If parsing fails, return a minimal version
            }
            
            return new System.Version(0, 0, 0, 0);
        }

        // Get the versions folder path - respects custom launcher data path
        private string GetVersionsFolder()
        {
            string basePath;
            
            // Use custom path if set, otherwise use default
            if (!string.IsNullOrEmpty(UserPrefs.LauncherDataPath))
            {
                basePath = UserPrefs.LauncherDataPath;
            }
            else
            {
                basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MinecraftVersionLauncher");
            }
            
            string versionsPath = Path.Combine(basePath, "Versions");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(versionsPath))
            {
                Directory.CreateDirectory(versionsPath);
            }
            
            return versionsPath;
        }

        // Open the versions folder in Windows Explorer
        private void ViewFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string versionsPath = GetVersionsFolder();
                Process.Start("explorer.exe", versionsPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open versions folder: " + ex.ToString());
                MessageBox.Show(
                    "Could not open the versions folder.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Tab Navigation
        private void TabChanged(object sender, RoutedEventArgs e)
        {
            // Null checks in case this fires during initialization
            if (MyVersionsContent == null || AvailableContent == null)
                return;

            MyVersionsContent.Visibility = MyVersionsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            AvailableContent.Visibility = AvailableTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        // Show version selection dialog with registered version highlighted in green
        private async Task<WPFDataTypes.Version> ShowVersionSelectionDialog(List<WPFDataTypes.Version> installedVersions, WPFDataTypes.Version registeredVersion)
        {
            var tcs = new TaskCompletionSource<WPFDataTypes.Version>();
            
            await Dispatcher.InvokeAsync(() =>
            {
                var dialog = new System.Windows.Window
                {
                    Title = Localization.Get("SelectMinecraftVersion"),
                    Width = 550,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A150C")),
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    ResizeMode = ResizeMode.NoResize
                };
                
                var mainBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#152818")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(16),
                    Margin = new Thickness(10)
                };
                
                var grid = new Grid { Margin = new Thickness(24) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                // Header
                var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                var titleText = new TextBlock
                {
                    Text = "🎮 " + Localization.Get("SelectVersionPrompt"),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleText, 0);
                headerGrid.Children.Add(titleText);
                
                // Close button
                var closeBtn = new Button
                {
                    Content = "✕",
                    Width = 32,
                    Height = 32,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0B5A3")),
                    BorderThickness = new Thickness(0),
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Cursor = Cursors.Hand
                };
                closeBtn.Click += (s, args) => { tcs.TrySetResult(null); dialog.Close(); };
                Grid.SetColumn(closeBtn, 1);
                headerGrid.Children.Add(closeBtn);
                
                Grid.SetRow(headerGrid, 0);
                grid.Children.Add(headerGrid);
                
                // Version list
                var listBox = new ListBox
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1B0F")),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                    BorderThickness = new Thickness(1),
                    FontSize = 14,
                    Padding = new Thickness(8)
                };
                
                int selectedIndex = 0;
                WPFDataTypes.Version bestVersion = DetermineBestVersionToSelect(installedVersions, registeredVersion);
                
                for (int i = 0; i < installedVersions.Count; i++)
                {
                    var version = installedVersions[i];
                    bool isRegistered = registeredVersion != null && version == registeredVersion;
                    bool isBestMatch = bestVersion != null && version == bestVersion;
                    
                    var itemGrid = new Grid();
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    var versionText = new TextBlock
                    {
                        Text = (version.VersionType == VersionType.Preview ? "✨ " : "⭐ ") + version.Name,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(versionText, 0);
                    itemGrid.Children.Add(versionText);
                    
                    // Show indicator for best match (green) or registered (yellow)
                    if (isBestMatch || isRegistered)
                    {
                        var indicator = new Ellipse
                        {
                            Width = 12,
                            Height = 12,
                            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isBestMatch ? "#00FF00" : "#FFD700")),
                            Margin = new Thickness(8, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        
                        // Pulsing animation for best match
                        if (isBestMatch)
                        {
                            var animation = new System.Windows.Media.Animation.DoubleAnimation
                            {
                                From = 1.0,
                                To = 0.3,
                                Duration = TimeSpan.FromSeconds(0.8),
                                AutoReverse = true,
                                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                            };
                            indicator.BeginAnimation(Ellipse.OpacityProperty, animation);
                        }
                        
                        Grid.SetColumn(indicator, 1);
                        itemGrid.Children.Add(indicator);
                        
                        if (isBestMatch)
                        {
                            selectedIndex = i; // Auto-select best match
                        }
                    }
                    
                    var item = new ListBoxItem
                    {
                        Content = itemGrid,
                        Padding = new Thickness(12, 10, 12, 10),
                        Margin = new Thickness(0, 0, 0, 4),
                        Tag = version
                    };
                    
                    // Green highlight for best match, subtle yellow for registered
                    if (isBestMatch)
                    {
                        item.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3D1F"));
                    }
                    else if (isRegistered)
                    {
                        item.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A1A"));
                    }
                    
                    listBox.Items.Add(item);
                }
                
                listBox.SelectedIndex = selectedIndex;
                Grid.SetRow(listBox, 1);
                grid.Children.Add(listBox);
                
                // Button panel
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                
                var cancelButton = new Button
                {
                    Content = Localization.Get("Cancel"),
                    Width = 120,
                    Height = 40,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D3520")),
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                cancelButton.Click += (s, args) => { tcs.TrySetResult(null); dialog.Close(); };
                
                var okButton = new Button
                {
                    Content = "▶️ " + Localization.Get("Launch"),
                    Width = 120,
                    Height = 40,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                okButton.Click += (s, args) =>
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        var selectedItem = listBox.Items[listBox.SelectedIndex] as ListBoxItem;
                        tcs.TrySetResult(selectedItem?.Tag as WPFDataTypes.Version);
                    }
                    else
                    {
                        tcs.TrySetResult(null);
                    }
                    dialog.Close();
                };
                
                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(okButton);
                Grid.SetRow(buttonPanel, 2);
                grid.Children.Add(buttonPanel);
                
                mainBorder.Child = grid;
                dialog.Content = mainBorder;
                
                // Allow dragging
                mainBorder.MouseLeftButtonDown += (s, ev) => { if (ev.ClickCount == 1) dialog.DragMove(); };
                
                dialog.ShowDialog();
            });
            
            return await tcs.Task;
        }

        // Custom Window Controls
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                if (WindowState == WindowState.Maximized)
                {
                    // Get mouse position relative to screen
                    var mousePos = PointToScreen(e.GetPosition(this));
                    
                    // Restore window
                    WindowState = WindowState.Normal;
                    
                    // Position window under cursor
                    Left = mousePos.X - (RestoreBounds.Width / 2);
                    Top = mousePos.Y - 20;
                }
                
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignore any drag exceptions
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Language switching
        private void SwitchToEnglish_Click(object sender, RoutedEventArgs e)
        {
            SetLanguage("en", showMessage: true);
        }

        private void SwitchToArabic_Click(object sender, RoutedEventArgs e)
        {
            SetLanguage("ar", showMessage: true);
        }

        private void SetLanguage(string languageCode, bool showMessage = false)
        {
            // Set localization language
            Localization.SetLanguage(languageCode);
            
            // Update button styles to highlight selected language
            if (languageCode == "en")
            {
                EnglishButton.Foreground = new SolidColorBrush(Color.FromRgb(0, 227, 252)); // #00e3fc
                EnglishButton.FontWeight = FontWeights.SemiBold;
                ArabicButton.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray
                ArabicButton.FontWeight = FontWeights.Normal;
                
                // Set LTR flow direction for English
                this.FlowDirection = FlowDirection.LeftToRight;
            }
            else if (languageCode == "ar")
            {
                ArabicButton.Foreground = new SolidColorBrush(Color.FromRgb(0, 227, 252)); // #00e3fc
                ArabicButton.FontWeight = FontWeights.SemiBold;
                EnglishButton.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray
                EnglishButton.FontWeight = FontWeights.Normal;
                
                // Set RTL flow direction for Arabic
                this.FlowDirection = FlowDirection.RightToLeft;
            }

            // Update all UI text
            UpdateUIText();

            // Save preference
            UserPrefs.Language = languageCode;
            RewritePrefs();

            // Show message only when user manually switches
            if (showMessage)
            {
                MessageBox.Show(
                    Localization.Get("LanguageChangedMessage"),
                    Localization.Get("LanguageChanged"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void UpdateUIText()
        {
            // Update window title
            this.Title = Localization.Get("AppTitle");
            
            // Update navigation
            if (NavigationLabel != null)
                NavigationLabel.Text = Localization.Get("Navigation");
            
            MyVersionsTab.Content = Localization.Get("MyVersions");
            AvailableTab.Content = Localization.Get("Browse");
            
            // Update My Versions section
            if (MyVersionsHeaderText != null)
                MyVersionsHeaderText.Text = Localization.Get("MyVersionsHeader");
            if (MyVersionsSubtitleText != null)
                MyVersionsSubtitleText.Text = Localization.Get("MyVersionsSubtitle");
            if (ImportButton != null)
                ImportButton.Content = Localization.Get("ImportFile");
            if (ViewFilesButton != null)
                ViewFilesButton.Content = Localization.Get("ViewFiles");
            
            // Update empty state
            if (EmptyStateTitle != null)
                EmptyStateTitle.Text = Localization.Get("NoVersionsInstalled");
            if (EmptyStateSubtitle != null)
                EmptyStateSubtitle.Text = Localization.Get("NoVersionsSubtitle");
            if (BrowseVersionsButton != null)
                BrowseVersionsButton.Content = Localization.Get("BrowseVersionsButton");
            
            // Update Browse section
            if (RefreshButton != null)
            {
                var refreshLabel = Localization.Get("Refresh");
                if (refreshLabel.StartsWith("🔄 "))
                    refreshLabel = refreshLabel.Substring("🔄 ".Length);
                RefreshButton.Content = refreshLabel;
            }
            
            // Update filter buttons
            FilterAll.Content = Localization.Get("All");
            FilterRelease.Content = Localization.Get("Release");
            FilterPreview.Content = Localization.Get("Preview");
            
            // Update loading text if visible
            if (LoadingText != null)
            {
                LoadingText.Text = Localization.Get("LoadingVersions");
            }
            
            // Update search box placeholder by clearing and resetting focus
            if (SearchBox != null)
            {
                var currentText = SearchBox.Text;
                SearchBox.Text = "";
                SearchBox.Text = currentText;
            }
            
            // Update status indicators
            UpdateStatusIndicators();
            
            // Update Settings button tooltip
            if (SettingsButton != null)
                SettingsButton.ToolTip = Localization.Get("SettingsTitle");
            
            // Update Made by text
            if (MadeByText != null)
                MadeByText.Text = Localization.Get("MadeBy");
            
            // Refresh all version view models to update localized text
            RefreshVersionViewModels();
        }
        
        private void RefreshVersionViewModels()
        {
            // Trigger property change notifications on all version view models
            // This updates localized properties like FriendlyStatus, PackageTypeDisplay, etc.
            foreach (var vm in _installedVersions)
            {
                vm.OnPropertyChanged("FriendlyStatus");
                vm.OnPropertyChanged("PackageTypeDisplay");
                vm.OnPropertyChanged("VersionTypeDisplay");
                vm.OnPropertyChanged("PlayButtonText");
                vm.OnPropertyChanged("DownloadButtonText");
                vm.OnPropertyChanged("RemoveTooltipText");
                vm.OnPropertyChanged("PauseResumeButtonText");
                vm.OnPropertyChanged("CancelButtonText");
            }
            
            foreach (var vm in _allVersions)
            {
                vm.OnPropertyChanged("FriendlyStatus");
                vm.OnPropertyChanged("PackageTypeDisplay");
                vm.OnPropertyChanged("VersionTypeDisplay");
                vm.OnPropertyChanged("PlayButtonText");
                vm.OnPropertyChanged("DownloadButtonText");
                vm.OnPropertyChanged("RemoveTooltipText");
                vm.OnPropertyChanged("PauseResumeButtonText");
                vm.OnPropertyChanged("CancelButtonText");
            }
        }

        private void UpdateStatusIndicators()
        {
            // Update indicator text based on language
            if (DevModeText != null)
                DevModeText.Text = Localization.Get("DeveloperMode");
            
            if (DecryptionKeysText != null)
                DecryptionKeysText.Text = Localization.Get("DecryptionKeys");
        }

        private bool HasDecryptionKeys()
        {
            try
            {
                // Check if Minecraft license/keys exist by looking for the actual decryption key storage
                // Keys are stored in: C:\ProgramData\Microsoft\Windows\AppRepository\Packages\Microsoft.MinecraftUWP_*
                string appRepoPath = @"C:\ProgramData\Microsoft\Windows\AppRepository\Packages";
                
                if (Directory.Exists(appRepoPath))
                {
                    var minecraftDirs = Directory.GetDirectories(appRepoPath, "Microsoft.MinecraftUWP_*");
                    if (minecraftDirs.Length > 0)
                    {
                        Debug.WriteLine($"Decryption Keys: Found {minecraftDirs.Length} Minecraft package(s) in AppRepository");
                        return true;
                    }
                }
                
                // Alternative check: Look for installed Minecraft package
                var packageManager = new Windows.Management.Deployment.PackageManager();
                var packages = packageManager.FindPackagesForUser("");
                
                foreach (var package in packages)
                {
                    if (package.Id.FamilyName == "Microsoft.MinecraftUWP_8wekyb3d8bbwe")
                    {
                        Debug.WriteLine($"Decryption Keys: Found installed Minecraft package - {package.Id.FullName}");
                        return true;
                    }
                }
                
                Debug.WriteLine("Decryption Keys: Not found");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decryption Keys check failed: {ex.Message}");
            }
            return false;
        }



        private bool IsDeveloperModeEnabled()
        {
            try
            {
                // Check both possible registry locations
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("AllowDevelopmentWithoutDevLicense");
                        if (value != null && (int)value == 1)
                        {
                            Debug.WriteLine("Developer Mode: Enabled (AllowDevelopmentWithoutDevLicense = 1)");
                            return true;
                        }
                    }
                }
                
                // Also check AllowAllTrustedApps as fallback
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("AllowAllTrustedApps");
                        if (value != null && (int)value == 1)
                        {
                            Debug.WriteLine("Developer Mode: Enabled (AllowAllTrustedApps = 1)");
                            return true;
                        }
                    }
                }
                
                Debug.WriteLine("Developer Mode: Disabled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Developer Mode check failed: {ex.Message}");
            }
            return false;
        }


        private void DevModeIndicator_Click(object sender, MouseButtonEventArgs e)
        {
            if (!IsDeveloperModeEnabled())
            {
                var result = MessageBox.Show(
                    Localization.Get("DevModeRequiredMessage"),
                    Localization.Get("DevModeRequired"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.OK)
                {
                    EnableDeveloperMode();
                }
            }
        }

        private void EnableDeveloperMode()
        {
            try
            {
                // Try to enable Developer Mode via registry
                // This requires admin privileges
                var psi = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppModelUnlock\" /v AllowDevelopmentWithoutDevLicense /t REG_DWORD /d 1 /f",
                    Verb = "runas", // Request admin elevation
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                
                var process = Process.Start(psi);
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    MessageBox.Show(
                        Localization.Get("DevModeEnabledMessage"),
                        Localization.Get("DevModeEnabled"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    ShowDevModeManualInstructions();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to enable Developer Mode: {ex.Message}");
                ShowDevModeManualInstructions();
            }
        }

        private void ShowDevModeManualInstructions()
        {
            MessageBox.Show(
                Localization.Get("DevModeEnableFailedMessage"),
                Localization.Get("DevModeEnableFailed"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DecryptionKeysIndicator_Click(object sender, MouseButtonEventArgs e)
        {
            if (!HasDecryptionKeys())
            {
                var result = MessageBox.Show(
                    Localization.Get("DecryptKeysRequiredMessage"),
                    Localization.Get("DecryptKeysRequired"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.OK)
                {
                    // Open Microsoft Store to Minecraft page
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "ms-windows-store://pdp/?ProductId=9NBLGGH2JHXJ",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to open Store: {ex.Message}");
                    }
                }
            }
            // If keys are installed, do nothing (indicator is not clickable)
        }

        // Native Windows mouse scrolling support
        private static readonly DependencyProperty SmoothScrollOffsetProperty =
            DependencyProperty.RegisterAttached("SmoothScrollOffset", typeof(double), typeof(ModernMainWindow), new PropertyMetadata(0.0, SmoothScrollOffsetChanged));

        private static void SmoothScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
                sv.ScrollToVerticalOffset((double)e.NewValue);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    return t;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null && sender is DependencyObject d)
                scrollViewer = FindVisualChild<ScrollViewer>(d);
            if (scrollViewer == null)
                return;

            double target = scrollViewer.VerticalOffset - e.Delta * 0.5;
            target = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, target));

            var anim = new DoubleAnimation(scrollViewer.VerticalOffset, target, TimeSpan.FromMilliseconds(250));
            anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            scrollViewer.BeginAnimation(SmoothScrollOffsetProperty, anim);
            e.Handled = true;
        }

        // Auto-scroll support - Middle mouse button
        private void ScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                ScrollViewer scrollViewer = sender as ScrollViewer;
                if (scrollViewer != null && !_isAutoScrolling)
                {
                    _isAutoScrolling = true;
                    _autoScrollViewer = scrollViewer;
                    _autoScrollStartPoint = e.GetPosition(scrollViewer);
                    
                    // Create and show scroll indicator
                    CreateAutoScrollIndicator(scrollViewer);
                    
                    // Capture mouse
                    scrollViewer.CaptureMouse();
                    
                    // Start auto-scroll timer
                    _autoScrollTimer.Start();
                    
                    e.Handled = true;
                }
            }
        }

        private void ScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && _isAutoScrolling)
            {
                StopAutoScroll();
                e.Handled = true;
            }
        }

        private void ScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isAutoScrolling && _autoScrollViewer != null)
            {
                // Update indicator position if needed
                Point currentPos = e.GetPosition(_autoScrollViewer);
                UpdateAutoScrollIndicator(currentPos);
            }
        }

        private void CreateAutoScrollIndicator(ScrollViewer scrollViewer)
        {
            // Create visual indicator for auto-scroll
            _autoScrollIndicator = new Ellipse
            {
                Width = 40,
                Height = 40,
                Fill = new SolidColorBrush(Color.FromArgb(180, 74, 74, 74)),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };

            // Add directional arrows
            var canvas = new Canvas
            {
                Width = 40,
                Height = 40,
                IsHitTestVisible = false
            };

            canvas.Children.Add(_autoScrollIndicator);

            // Add arrow indicators
            var upArrow = new ShapePath
            {
                Data = Geometry.Parse("M 20,12 L 16,18 L 24,18 Z"),
                Fill = Brushes.White,
                IsHitTestVisible = false
            };
            var downArrow = new ShapePath
            {
                Data = Geometry.Parse("M 20,28 L 16,22 L 24,22 Z"),
                Fill = Brushes.White,
                IsHitTestVisible = false
            };

            canvas.Children.Add(upArrow);
            canvas.Children.Add(downArrow);

            // Position the indicator
            Canvas.SetLeft(canvas, _autoScrollStartPoint.X - 20);
            Canvas.SetTop(canvas, _autoScrollStartPoint.Y - 20);
            Canvas.SetZIndex(canvas, 9999);

            // Add to the scroll viewer's parent grid
            var grid = scrollViewer.Parent as Grid;
            if (grid != null)
            {
                grid.Children.Add(canvas);
                _autoScrollIndicator.Tag = canvas; // Store reference for removal
            }
        }

        private void UpdateAutoScrollIndicator(Point currentPos)
        {
            // Visual feedback could be added here if needed
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (!_isAutoScrolling || _autoScrollViewer == null)
            {
                StopAutoScroll();
                return;
            }

            try
            {
                Point currentPos = Mouse.GetPosition(_autoScrollViewer);
                double deltaY = currentPos.Y - _autoScrollStartPoint.Y;
                
                // Calculate scroll speed based on distance from start point
                double scrollSpeed = 0;
                double deadZone = 10; // Pixels of dead zone around start point
                
                if (Math.Abs(deltaY) > deadZone)
                {
                    // Exponential scaling for more natural feel
                    scrollSpeed = Math.Sign(deltaY) * Math.Pow(Math.Abs(deltaY) - deadZone, 1.2) * 0.05;
                    
                    // Clamp maximum speed
                    scrollSpeed = Math.Max(-50, Math.Min(50, scrollSpeed));
                }

                if (scrollSpeed != 0)
                {
                    double newOffset = _autoScrollViewer.VerticalOffset + scrollSpeed;
                    _autoScrollViewer.ScrollToVerticalOffset(newOffset);
                }
            }
            catch
            {
                StopAutoScroll();
            }
        }

        private void StopAutoScroll()
        {
            _isAutoScrolling = false;
            _autoScrollTimer.Stop();

            if (_autoScrollViewer != null)
            {
                _autoScrollViewer.ReleaseMouseCapture();
                
                // Remove indicator
                if (_autoScrollIndicator != null && _autoScrollIndicator.Tag is Canvas canvas)
                {
                    var grid = _autoScrollViewer.Parent as Grid;
                    if (grid != null && grid.Children.Contains(canvas))
                    {
                        grid.Children.Remove(canvas);
                    }
                }
                
                _autoScrollViewer = null;
            }

            _autoScrollIndicator = null;
        }

        private void GoToAvailable_Click(object sender, RoutedEventArgs e)
        {
            AvailableTab.IsChecked = true;
        }

        // Filter and Search
        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            if (_allVersions == null || _allVersions.Count == 0)
                return;
                
            _ = ApplyFiltersAsync();
        }

        private System.Threading.CancellationTokenSource _searchCts;
        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;
            try 
            {
                await Task.Delay(250, token);
                await ApplyFiltersAsync();
            } 
            catch (TaskCanceledException) { }
        }

        private async Task ApplyFiltersAsync()
        {
            if (_allVersions == null || _allVersions.Count == 0)
                return;

            var searchText = SearchBox.Text?.ToLower() ?? "";
            VersionType? filterType = null;

            if (FilterRelease.IsChecked == true)
                filterType = VersionType.Release;
            else if (FilterPreview.IsChecked == true)
                filterType = VersionType.Preview;

            var filtered = await Task.Run(() => _allVersions.Where(v =>
            {
                if (filterType.HasValue && v.Version.VersionType != filterType.Value)
                    return false;
                if (!string.IsNullOrWhiteSpace(searchText) && !v.Version.DisplayName.ToLower().Contains(searchText))
                    return false;
                return true;
            }).ToList());

            // Feed the filtered set into the paging window (keep the bound instance stable)
            _pagedFull = filtered;
            _pageStart = 0;
            RenderPage();
        }

        private async void RefreshVersions_Click(object sender, RoutedEventArgs e)
        {
            await LoadVersionList();
        }

        private volatile bool _hasImportTask = false;

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            // Block concurrent imports
            if (_hasImportTask)
            {
                MessageBox.Show(
                    Localization.Get("ImportInProgressMessage"), 
                    Localization.Get("ImportInProgress"), 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
                return;
            }
            
            _hasImportTask = true;
            
            try
            {
                Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();
                openFileDlg.Filter = "Minecraft Packages (*.msixvc, *.appx)|*.msixvc;*.appx|All Files|*.*";
                openFileDlg.Title = "Choose a Minecraft package file";
                
                Nullable<bool> result = openFileDlg.ShowDialog();
                
                if (result == true)
                {
                    string directory = Path.Combine(IMPORTED_VERSIONS_PATH, openFileDlg.SafeFileName);
                    
                    // Check if already exists
                    if (Directory.Exists(directory))
                    {
                        var existingVersion = _versions.FirstOrDefault(v => v.IsImported && v.GameDirectory == directory);
                        if (existingVersion != null)
                        {
                            if (existingVersion.IsStateChanging)
                            {
                                ShowFriendlyError(Localization.Get("PleaseWait"), Localization.Get("PleaseWaitMessage"));
                                return;
                            }

                            var confirmResult = MessageBox.Show(
                                Localization.Get("ReplaceExistingMessage"),
                                Localization.Get("ReplaceExisting"),
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (confirmResult == MessageBoxResult.Yes)
                            {
                                var removeResult = await RemoveVersion(existingVersion);
                                if (!removeResult)
                                {
                                    ShowFriendlyError(Localization.Get("CouldntRemoveOld"), Localization.Get("CouldntRemoveOldMessage"));
                                    return;
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                    }

                    var extension = Path.GetExtension(openFileDlg.FileName).ToLowerInvariant();
                    PackageType packageType;
                    
                    if (extension == ".msixvc")
                    {
                        packageType = PackageType.GDK;
                    }
                    else if (extension == ".appx")
                    {
                        packageType = PackageType.UWP;
                    }
                    else
                    {
                        ShowFriendlyError(Localization.Get("WrongFileType"), Localization.Format("WrongFileTypeMessage", extension));
                        return;
                    }

                    var versionEntry = _versions.AddEntry(openFileDlg.SafeFileName, directory, packageType);
                    
                    // Show visual feedback - switch to My Versions tab and refresh to show new entry
                    MyVersionsTab.IsChecked = true;
                    
                    RefreshVersionLists(); // Creates ViewModels for all versions including the new one
                    
                    // Find the ViewModel that was just created for this version
                    var viewModel = _installedVersions.FirstOrDefault(vm => vm.Version == versionEntry);
                    if (viewModel != null)
                    {
                        // Set state on the actual version object (the ViewModel will reflect this)
                        versionEntry.StateChangeInfo = new VersionStateChangeInfo(VersionState.Extracting);
                        versionEntry.StateChangeInfo.Progress = 0;
                        versionEntry.StateChangeInfo.MaxProgress = 0; // Indeterminate progress
                    }
                    
                    // CRITICAL: Force UI update before starting heavy extraction work
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    
                    bool success = false;

                    if (packageType == PackageType.UWP)
                    {
                        success = await ExtractAppx(openFileDlg.FileName, directory, versionEntry);
                    }
                    else if (packageType == PackageType.GDK)
                    {
                        if (!ShowGDKFirstUseWarning())
                        {
                            success = false;
                        }
                        else
                        {
                            success = await ExtractMsixvc(openFileDlg.FileName, directory, versionEntry, isPreview: false);
                        }
                    }

                    if (success)
                    {
                        versionEntry.StateChangeInfo = null;
                        versionEntry.UpdateInstallStatus();
                        ShowFriendlySuccess(Localization.Get("VersionAdded"), Localization.Format("VersionAddedMessage", versionEntry.DisplayName));
                    }
                    else
                    {
                        _versions.Remove(versionEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed with error:\n\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _hasImportTask = false;
            }
        }

        // Helper methods from original MainWindow
        private void ShowFriendlyError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowFriendlySuccess(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ShowFriendlyInfo(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool ShowGDKFirstUseWarning()
        {
            if (!UserPrefs.HasPreviouslyUsedGDK)
            {
                var result = MessageBox.Show(
                    Localization.Get("GDKWarningMessage"),
                    Localization.Get("FirstTimeSetup"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.OK)
                {
                    UserPrefs.HasPreviouslyUsedGDK = true;
                    RewritePrefs();
                    return true;
                }
                return false;
            }
            return true;
        }

        private void RewritePrefs()
        {
            // CRITICAL: Lock to prevent concurrent writes corrupting preferences.json
            lock (_prefsLock) {
                File.WriteAllText(PREFS_PATH, JsonConvert.SerializeObject(UserPrefs, Formatting.Indented));
            }
        }
        
        // Update download indicator in title bar
        private void UpdateDownloadIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                int count = _activeDownloads.Count;
                if (count > 0)
                {
                    DownloadIndicator.Visibility = Visibility.Visible;
                    DownloadCountText.Text = count.ToString();
                }
                else
                {
                    DownloadIndicator.Visibility = Visibility.Collapsed;
                }
            });
        }
        
        private void DownloadIndicator_Click(object sender, MouseButtonEventArgs e)
        {
            // Show popup with currently downloading versions
            ShowActiveDownloadsDialog();
        }
        
        private void ShowActiveDownloadsDialog()
        {
            // If dialog already open, just bring it to front
            if (_activeDownloadsDialog != null && _activeDownloadsDialog.IsVisible)
            {
                _activeDownloadsDialog.Activate();
                return;
            }
            
            var dialog = new Window
            {
                Title = Localization.Get("ActiveDownloads"),
                Width = 750,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = Brushes.Transparent,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize
            };
            
            _activeDownloadsDialog = dialog;

            var mainBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#152818")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16)
            };

            var grid = new Grid { Margin = new Thickness(24) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header with close button
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = "📥 " + Localization.Get("ActiveDownloads"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            headerGrid.Children.Add(titleText);

            // Close button (X)
            var closeButtonTop = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0B5A3")),
                BorderThickness = new Thickness(0),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeButtonTop.Click += (s, args) => {
                StopDownloadDialogUpdates();
                dialog.Close();
            };
            
            var closeButtonTopTemplate = new ControlTemplate(typeof(Button));
            var closeButtonBorder = new FrameworkElementFactory(typeof(Border));
            closeButtonBorder.Name = "border";
            closeButtonBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            closeButtonBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            closeButtonBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            closeButtonBorder.AppendChild(contentPresenter);
            
            closeButtonTopTemplate.VisualTree = closeButtonBorder;
            
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D3520")), "border"));
            closeButtonTopTemplate.Triggers.Add(hoverTrigger);
            
            closeButtonTop.Template = closeButtonTopTemplate;
            
            Grid.SetColumn(closeButtonTop, 1);
            headerGrid.Children.Add(closeButtonTop);

            Grid.SetRow(headerGrid, 0);
            grid.Children.Add(headerGrid);

            // Downloads list with custom scrollbar
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1B0F")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12)
            };
            Grid.SetRow(scrollViewer, 1);

            var downloadsPanel = new StackPanel();
            downloadsPanel.Name = "DownloadsPanel"; // Name it so we can find it later
            scrollViewer.Content = downloadsPanel;
            grid.Children.Add(scrollViewer);

            // Bottom button panel
            var bottomButtonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            var closeButton = new Button
            {
                Content = "✓ " + Localization.Get("Close"),
                Width = 120,
                Height = 40,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            closeButton.Click += (s, args) => {
                StopDownloadDialogUpdates();
                dialog.Close();
            };
            
            var closeButtonTemplate = new ControlTemplate(typeof(Button));
            var closeBorder = new FrameworkElementFactory(typeof(Border));
            closeBorder.Name = "border";
            closeBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            closeBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            closeBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            
            var closeContent = new FrameworkElementFactory(typeof(ContentPresenter));
            closeContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            closeContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            closeBorder.AppendChild(closeContent);
            
            closeButtonTemplate.VisualTree = closeBorder;
            
            var closeHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            closeHoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A8534")), "border"));
            closeButtonTemplate.Triggers.Add(closeHoverTrigger);
            
            closeButton.Template = closeButtonTemplate;
            
            bottomButtonPanel.Children.Add(closeButton);
            Grid.SetRow(bottomButtonPanel, 2);
            grid.Children.Add(bottomButtonPanel);

            mainBorder.Child = grid;
            dialog.Content = mainBorder;
            
            // Allow dragging the window
            mainBorder.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 1) dialog.DragMove(); };
            
            // Handle dialog closing
            dialog.Closed += (s, e) => {
                StopDownloadDialogUpdates();
                _activeDownloadsDialog = null;
            };
            
            // Initial population
            UpdateDownloadDialogContent(downloadsPanel);
            
            // Start live updates timer (updates every 500ms)
            StartDownloadDialogUpdates(downloadsPanel);
            
            dialog.Show();
        }
        
        private void StartDownloadDialogUpdates(StackPanel downloadsPanel)
        {
            if (_downloadDialogUpdateTimer != null)
            {
                _downloadDialogUpdateTimer.Stop();
                _downloadDialogUpdateTimer = null;
            }
            
            _downloadDialogUpdateTimer = new System.Windows.Threading.DispatcherTimer();
            _downloadDialogUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _downloadDialogUpdateTimer.Tick += (s, e) => UpdateDownloadDialogContent(downloadsPanel);
            _downloadDialogUpdateTimer.Start();
        }
        
        private void StopDownloadDialogUpdates()
        {
            if (_downloadDialogUpdateTimer != null)
            {
                _downloadDialogUpdateTimer.Stop();
                _downloadDialogUpdateTimer = null;
            }
        }
        
        private void UpdateDownloadDialogContent(StackPanel downloadsPanel)
        {
            if (downloadsPanel == null) return;
            
            downloadsPanel.Children.Clear();
            
            lock (_downloadLock)
            {
                if (_activeDownloads.Count == 0)
                {
                    var emptyText = new TextBlock
                    {
                        Text = "📭 " + Localization.Get("NoActiveDownloads"),
                        FontSize = 16,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0B5A3")),
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 80, 0, 0)
                    };
                    downloadsPanel.Children.Add(emptyText);
                }
                else
                {
                    foreach (var version in _activeDownloads.ToList())
                    {
                        var card = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#152818")),
                            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(12),
                            Padding = new Thickness(20),
                            Margin = new Thickness(0, 0, 0, 12)
                        };

                        var cardGrid = new Grid();
                        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        // Version name with icon
                        var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                        
                        var iconText = new TextBlock
                        {
                            Text = version.VersionType == VersionType.Preview ? "✨" : "⭐",
                            FontSize = 18,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        namePanel.Children.Add(iconText);
                        
                        var nameText = new TextBlock
                        {
                            Text = version.DisplayName,
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        namePanel.Children.Add(nameText);
                        
                        Grid.SetRow(namePanel, 0);
                        cardGrid.Children.Add(namePanel);

                        // Progress info (with proper RTL support for Arabic)
                        if (version.StateChangeInfo != null)
                        {
                            var progressText = new TextBlock
                            {
                                Text = version.StateChangeInfo.DisplayStatus,
                                FontSize = 14,
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4FF")),
                                Margin = new Thickness(0, 0, 0, 10),
                                FlowDirection = Localization.GetCurrentLanguage() == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
                            };
                            Grid.SetRow(progressText, 1);
                            cardGrid.Children.Add(progressText);

                            // Progress bar
                            var progressBar = new System.Windows.Controls.ProgressBar
                            {
                                Height = 8,
                                Minimum = 0,
                                Maximum = version.StateChangeInfo.MaxProgress > 0 ? version.StateChangeInfo.MaxProgress : 100,
                                Value = version.StateChangeInfo.Progress,
                                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1B0F")),
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                                BorderThickness = new Thickness(1),
                                IsIndeterminate = version.StateChangeInfo.IsProgressIndeterminate,
                                Margin = new Thickness(0, 0, 0, 12)
                            };
                            Grid.SetRow(progressBar, 2);
                            cardGrid.Children.Add(progressBar);
                            
                            // Action buttons
                            var buttonPanel = new StackPanel 
                            { 
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right
                            };
                            
                            // Pause/Resume button
                            var pauseButton = new Button
                            {
                                Content = version.StateChangeInfo.IsPaused ? Localization.Get("Resume") : Localization.Get("Pause"),
                                Height = 32,
                                Padding = new Thickness(16, 0, 16, 0),
                                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
                                Foreground = Brushes.White,
                                BorderThickness = new Thickness(0),
                                FontSize = 13,
                                FontWeight = FontWeights.SemiBold,
                                Cursor = Cursors.Hand,
                                Margin = new Thickness(0, 0, 8, 0)
                            };
                            pauseButton.Click += (s, args) => InvokePauseResume(version);
                            
                            var pauseTemplate = new ControlTemplate(typeof(Button));
                            var pauseBorder = new FrameworkElementFactory(typeof(Border));
                            pauseBorder.Name = "border";
                            pauseBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                            pauseBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                            pauseBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
                            var pauseContent = new FrameworkElementFactory(typeof(ContentPresenter));
                            pauseContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                            pauseContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                            pauseBorder.AppendChild(pauseContent);
                            pauseTemplate.VisualTree = pauseBorder;
                            var pauseHover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                            pauseHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")), "border"));
                            pauseTemplate.Triggers.Add(pauseHover);
                            pauseButton.Template = pauseTemplate;
                            
                            buttonPanel.Children.Add(pauseButton);
                            
                            // Cancel button
                            var cancelButton = new Button
                            {
                                Content = Localization.Get("Cancel"),
                                Height = 32,
                                Padding = new Thickness(16, 0, 16, 0),
                                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3B5C")),
                                Foreground = Brushes.White,
                                BorderThickness = new Thickness(0),
                                FontSize = 13,
                                FontWeight = FontWeights.SemiBold,
                                Cursor = Cursors.Hand
                            };
                            cancelButton.Click += (s, args) => 
                            {
                                if (version.StateChangeInfo?.CancelCommand != null)
                                {
                                    version.StateChangeInfo.CancelCommand.Execute(null);
                                }
                            };
                            
                            var cancelTemplate = new ControlTemplate(typeof(Button));
                            var cancelBorder = new FrameworkElementFactory(typeof(Border));
                            cancelBorder.Name = "border";
                            cancelBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                            cancelBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                            cancelBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
                            var cancelContent = new FrameworkElementFactory(typeof(ContentPresenter));
                            cancelContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                            cancelContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                            cancelBorder.AppendChild(cancelContent);
                            cancelTemplate.VisualTree = cancelBorder;
                            var cancelHover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                            cancelHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")), "border"));
                            cancelTemplate.Triggers.Add(cancelHover);
                            cancelButton.Template = cancelTemplate;
                            
                            buttonPanel.Children.Add(cancelButton);
                            
                            Grid.SetRow(buttonPanel, 3);
                            cardGrid.Children.Add(buttonPanel);
                        }

                        card.Child = cardGrid;
                        downloadsPanel.Children.Add(card);
                    }
                }
            }
        }
        
        private void AddActiveDownload(WPFDataTypes.Version v)
        {
            lock (_downloadLock)
            {
                _activeDownloads.Add(v);
                UpdateDownloadIndicator();
            }
        }
        
        private void RemoveActiveDownload(WPFDataTypes.Version v)
        {
            lock (_downloadLock)
            {
                _activeDownloads.Remove(v);
                UpdateDownloadIndicator();
            }
        }

        // Settings button handler
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsDialog();
        }

        private void ShowSettingsDialog()
        {
            var dialog = new Window
            {
                Title = Localization.Get("SettingsTitle"),
                Width = 600,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#152818")),
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titleText = new TextBlock
            {
                Text = Localization.Get("LauncherSettings"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);

            // Data path section
            var pathLabel = new TextBlock
            {
                Text = Localization.Get("LauncherDataPath"),
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0B5A3")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(pathLabel, 2);
            grid.Children.Add(pathLabel);

            var pathPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            Grid.SetRow(pathPanel, 3);

            var pathTextBox = new TextBox
            {
                Text = string.IsNullOrEmpty(UserPrefs.LauncherDataPath) 
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinecraftVersionLauncher")
                    : UserPrefs.LauncherDataPath,
                Width = 400,
                Height = 32,
                Padding = new Thickness(8),
                FontSize = 12,
                IsReadOnly = false,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1B0F")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
                AcceptsReturn = false,
                TextWrapping = TextWrapping.NoWrap
            };

            var browseButton = new Button
            {
                Content = Localization.Get("Browse"),
                Width = 100,
                Height = 32,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };

            browseButton.Click += (s, args) =>
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = Localization.Get("SelectLauncherDataFolder"),
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string newPath = folderDialog.SelectedPath;
                    pathTextBox.Text = newPath;
                }
            };

            pathPanel.Children.Add(pathTextBox);
            pathPanel.Children.Add(browseButton);
            grid.Children.Add(pathPanel);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            Grid.SetRow(buttonPanel, 4);

            var cancelButton = new Button
            {
                Content = Localization.Get("Cancel"),
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D3520")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, args) => dialog.Close();

            var saveButton = new Button
            {
                Content = Localization.Get("Save"),
                Width = 100,
                Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };

            saveButton.Click += async (s, args) =>
            {
                string newPath = pathTextBox.Text.Trim();
                
                // Validate path
                if (string.IsNullOrWhiteSpace(newPath))
                {
                    MessageBox.Show(
                        Localization.Get("PleaseEnterValidPath"),
                        Localization.Get("InvalidPath"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Check if path contains invalid characters
                try
                {
                    Path.GetFullPath(newPath); // This will throw if path is invalid
                }
                catch
                {
                    MessageBox.Show(
                        Localization.Get("PathContainsInvalidChars"),
                        Localization.Get("InvalidPath"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                string currentPath = string.IsNullOrEmpty(UserPrefs.LauncherDataPath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinecraftVersionLauncher")
                    : UserPrefs.LauncherDataPath;

                if (newPath != currentPath)
                {
                    var result = MessageBox.Show(
                        Localization.Format("ConfirmMoveMessage", currentPath, newPath),
                        Localization.Get("ConfirmDataPathChange"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        dialog.Close();
                        await MoveLauncherDataAsync(currentPath, newPath);
                    }
                }
                else
                {
                    dialog.Close();
                }
            };

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(saveButton);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        private async Task MoveLauncherDataAsync(string oldPath, string newPath)
        {
            Window progressDialog = null;
            TextBlock progressText = null;
            TextBlock percentText = null;
            System.Windows.Controls.ProgressBar progressBar = null;
            
            // Declare at method level to avoid scope conflicts
            string defaultLauncherPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MinecraftVersionLauncher");
            
            try
            {
                // Normalize paths for comparison
                oldPath = Path.GetFullPath(oldPath);
                newPath = Path.GetFullPath(newPath);
                
                // Check if paths are the same
                if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "Source and destination paths are the same.",
                        "No Changes Needed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                
                // Create progress dialog with better UI
                progressDialog = new Window
                {
                    Title = "Moving Data",
                    Width = 450,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#152818")),
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    AllowsTransparency = true
                };

                var progressGrid = new Grid { Margin = new Thickness(30) };
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
                progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                progressText = new TextBlock
                {
                    Text = "Preparing to move data...",
                    FontSize = 14,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(progressText, 0);
                
                percentText = new TextBlock
                {
                    Text = "0%",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4FF")),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                Grid.SetRow(percentText, 2);
                
                progressBar = new System.Windows.Controls.ProgressBar
                {
                    Height = 8,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1B0F")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4FF")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#216C2A")),
                    BorderThickness = new Thickness(1)
                };
                Grid.SetRow(progressBar, 4);
                
                progressGrid.Children.Add(progressText);
                progressGrid.Children.Add(percentText);
                progressGrid.Children.Add(progressBar);
                progressDialog.Content = progressGrid;
                progressDialog.Show();

                await Task.Run(() =>
                {
                    // Create new directory if it doesn't exist
                    if (!Directory.Exists(newPath))
                    {
                        Directory.CreateDirectory(newPath);
                    }

                    // Count total files for progress tracking
                    Dispatcher.Invoke(() => progressText.Text = "Counting files...");
                    
                    var allFiles = Directory.GetFiles(oldPath, "*.*", SearchOption.AllDirectories).ToList();
                    int totalFiles = allFiles.Count;
                    int processedFiles = 0;

                    // Create directory structure
                    Dispatcher.Invoke(() => progressText.Text = "Creating directories...");
                    foreach (string dirPath in Directory.GetDirectories(oldPath, "*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(dirPath.Replace(oldPath, newPath));
                    }

                    // Copy all files with progress
                    foreach (string filePath in allFiles)
                    {
                        string fileName = Path.GetFileName(filePath);
                        Dispatcher.Invoke(() => progressText.Text = $"Copying: {fileName}");
                        
                        string newFilePath = filePath.Replace(oldPath, newPath);
                        File.Copy(filePath, newFilePath, true);
                        
                        processedFiles++;
                        int percent = (int)((processedFiles / (double)totalFiles) * 100);
                        Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = percent;
                            percentText.Text = $"{percent}%";
                        });
                    }
                    
                    // Update preferences and save to NEW location BEFORE deleting old
                    Dispatcher.Invoke(() => progressText.Text = "Updating configuration...");
                    UserPrefs.LauncherDataPath = newPath;
                    string newPrefsPath = Path.Combine(newPath, "preferences.json");
                    File.WriteAllText(newPrefsPath, JsonConvert.SerializeObject(UserPrefs, Formatting.Indented));
                    
                    // Create redirect file in OLD location pointing to NEW location
                    // Only create redirect if we're moving FROM the default location
                    if (string.Equals(oldPath, defaultLauncherPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Ensure default directory exists
                        if (!Directory.Exists(defaultLauncherPath))
                        {
                            Directory.CreateDirectory(defaultLauncherPath);
                        }
                        
                        // Create redirect file
                        string redirectFile = Path.Combine(defaultLauncherPath, "MOVED_TO.txt");
                        File.WriteAllText(redirectFile, $"Launcher data has been moved to:\n{newPath}\n\nThis file is used by the launcher to find the new location.");
                        
                        // Also save a minimal preferences file with just the path
                        string redirectPrefs = Path.Combine(defaultLauncherPath, "preferences.json");
                        var redirectPrefsObj = new Preferences { LauncherDataPath = newPath };
                        File.WriteAllText(redirectPrefs, JsonConvert.SerializeObject(redirectPrefsObj, Formatting.Indented));
                    }
                });

                Dispatcher.Invoke(() =>
                {
                    progressDialog.Close();
                    progressDialog = null;
                });

                // Show success message
                MessageBox.Show(
                    Localization.Get("DataMovedSuccessfully"),
                    Localization.Get("Success"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Create a batch script to delete old directory after app closes and restart
                string batchPath = Path.Combine(Path.GetTempPath(), "cleanup_launcher.bat");
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                
                StringBuilder batchContent = new StringBuilder();
                batchContent.AppendLine("@echo off");
                batchContent.AppendLine("timeout /t 2 /nobreak > nul");
                
                // Only delete old directory if it's NOT the default location (we keep redirect there)
                if (!string.Equals(oldPath, defaultLauncherPath, StringComparison.OrdinalIgnoreCase))
                {
                    batchContent.AppendLine($"rd /s /q \"{oldPath}\" 2>nul");
                }
                else
                {
                    // If it's the default location, delete everything EXCEPT the redirect files
                    batchContent.AppendLine($"cd /d \"{oldPath}\"");
                    batchContent.AppendLine("for /d %%D in (*) do rd /s /q \"%%D\" 2>nul");
                    batchContent.AppendLine("for %%F in (*) do (");
                    batchContent.AppendLine("  if /i not \"%%F\"==\"MOVED_TO.txt\" (");
                    batchContent.AppendLine("    if /i not \"%%F\"==\"preferences.json\" (");
                    batchContent.AppendLine("      del \"%%F\" 2>nul");
                    batchContent.AppendLine("    )");
                    batchContent.AppendLine("  )");
                    batchContent.AppendLine(")");
                }
                
                batchContent.AppendLine($"start \"\" \"{exePath}\"");
                batchContent.AppendLine("del \"%~f0\"");
                
                File.WriteAllText(batchPath, batchContent.ToString());
                
                // Start the batch script and exit
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = batchPath,
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
                catch (Exception batchEx)
                {
                    Debug.WriteLine($"Failed to start cleanup batch: {batchEx}");
                }
                
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to move launcher data: {ex}");
                
                if (progressDialog != null)
                {
                    Dispatcher.Invoke(() => progressDialog.Close());
                }
                
                MessageBox.Show(
                    $"Failed to move launcher data:\n\n{ex.Message}\n\nThe old location has been preserved.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // CRITICAL: Helper to check if any version is in a package management state that blocks ALL operations
        private bool IsAnyVersionInCriticalState() {
            return _versions.Any(ver => 
                ver.StateChangeInfo?.VersionState == VersionState.Staging ||
                ver.StateChangeInfo?.VersionState == VersionState.Unregistering ||
                ver.StateChangeInfo?.VersionState == VersionState.Registering);
        }

        // ICommonVersionCommands implementation
        public ICommand LaunchCommand => new RelayCommand(
            (v) => InvokeLaunch((WPFDataTypes.Version)v),
            (v) => {
                var version = v as WPFDataTypes.Version;
                // CRITICAL: Disable launch when:
                // 1. Version is not installed
                // 2. This version is in a state change
                // 3. ANY version is in critical package management state (staging/unregistering/registering)
                return version != null && version.IsInstalled && !version.IsStateChanging && !IsAnyVersionInCriticalState();
            }
        );
        public ICommand RemoveCommand => new RelayCommand(
            (v) => InvokeRemove((WPFDataTypes.Version)v),
            (v) => {
                var version = v as WPFDataTypes.Version;
                // CRITICAL: Disable remove during state changes OR critical package operations
                return version != null && !version.IsStateChanging && !IsAnyVersionInCriticalState();
            }
        );
        public ICommand DownloadCommand => new RelayCommand(
            (v) => InvokeDownload((WPFDataTypes.Version)v),
            (v) => {
                var version = v as WPFDataTypes.Version;
                // CRITICAL: Disable download during state changes (except when paused) OR critical package operations
                return version != null && (!version.IsStateChanging || (version.StateChangeInfo?.IsPaused == true)) && !IsAnyVersionInCriticalState();
            }
        );
        public ICommand PauseResumeCommand => new RelayCommand(
            (v) => InvokePauseResume((WPFDataTypes.Version)v),
            (v) => {
                var version = v as WPFDataTypes.Version;
                // Only enabled during download state AND no critical operations
                return version != null && version.IsStateChanging && 
                       (version.StateChangeInfo?.VersionState == VersionState.Downloading || 
                        version.StateChangeInfo?.VersionState == VersionState.Initializing) &&
                       !IsAnyVersionInCriticalState();
            }
        );
        public ICommand UnlockCommand => new RelayCommand(
            (v) => InvokeUnlock((WPFDataTypes.Version)v),
            (v) => {
                var version = v as WPFDataTypes.Version;
                // CRITICAL: OnlineFix DLLs only work on GDK versions, so hide/disable for UWP/UMP
                return version != null && version.PackageType == PackageType.GDK && version.IsInstalled && !version.IsStateChanging && !IsAnyVersionInCriticalState();
            }
        );

        public ICommand FreeTrialUnlockCommand => new RelayCommand(
            (v) => InvokeFreeTrialUnlock((WPFDataTypes.Version)v),
            (v) => {
                var version = v as WPFDataTypes.Version;
                // CRITICAL: Free trial unlock is for non-GDK (UWP/UMP) versions only
                return version != null && version.PackageType == PackageType.UWP && version.IsInstalled && !version.IsStateChanging && !IsAnyVersionInCriticalState();
            }
        );

        private bool _isUnlocking = false;

        private async void InvokeUnlock(WPFDataTypes.Version v)
        {
            if (v == null || !v.IsInstalled || _isUnlocking)
                return;

            string gameDir = Path.GetFullPath(v.GameDirectory);

            if (BfixInjector.IsAlreadyUnlocked(gameDir))
            {
                ShowFriendlyInfo(
                    Localization.Get("AlreadyUnlocked"),
                    Localization.Format("AlreadyUnlockedMessage", v.DisplayName));
                return;
            }

            var result = MessageBox.Show(
                Localization.Get("UnlockConfirmMessage"),
                Localization.Get("UnlockConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            _isUnlocking = true;
            try
            {
                await BfixInjector.InjectToMinecraftAsync(gameDir);
                ShowFriendlySuccess(
                    Localization.Get("UnlockSuccess"),
                    Localization.Format("UnlockSuccessMessage", v.DisplayName));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unlock failed: {ex}");
                ShowFriendlyError(
                    Localization.Get("UnlockDownloadFailedTitle"),
                    Localization.Format("UnlockDownloadFailedMessage", ex.Message));
            }
            finally
            {
                _isUnlocking = false;
            }
        }

        private void InvokeFreeTrialUnlock(WPFDataTypes.Version v)
        {
            if (v == null)
                return;

            if (TrialUnlockHelper.IsTrialUnlocked())
            {
                ShowFriendlyInfo(
                    Localization.Get("TrialAlreadyUnlocked"),
                    Localization.Format("TrialAlreadyUnlockedMessage", v.DisplayName));
                return;
            }

            var result = MessageBox.Show(
                Localization.Get("TrialUnlockConfirmMessage"),
                Localization.Get("TrialUnlockConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (!TrialUnlockHelper.IsAdministrator())
                {
                    TrialUnlockHelper.RunElevatedAndWait();
                }
                else
                {
                    TrialUnlockHelper.ApplyTrialUnlock();
                }

                if (TrialUnlockHelper.IsTrialUnlocked())
                {
                    ShowFriendlySuccess(
                        Localization.Get("TrialUnlockSuccess"),
                        Localization.Format("TrialUnlockSuccessMessage", v.DisplayName));
                }
                else
                {
                    ShowFriendlyError(
                        Localization.Get("Error"),
                        Localization.Get("TrialUnlockFailedMessage"));
                }
            }
            catch (Exception ex)
            {
                ShowFriendlyError(
                    Localization.Get("Error"),
                    $"Failed to unlock free trial:\n\n{ex.Message}");
            }
        }

        private void InvokePauseResume(WPFDataTypes.Version v)
        {
            if (!v.IsStateChanging || v.StateChangeInfo == null)
                return;

            if (v.StateChangeInfo.IsPaused)
            {
                // Resume download - DON'T modify state here, let InvokeDownload handle it
                // Just clear the paused flag so InvokeDownload knows this is a resume
                Debug.WriteLine("Resuming download for: " + v.DisplayName);
                InvokeDownload(v);
            }
            else
            {
                // Pause download by canceling it (file will be kept for resume)
                Debug.WriteLine("Pausing download for: " + v.DisplayName);
                v.StateChangeInfo.IsPaused = true;
                if (_downloadCancelTokens.ContainsKey(v))
                {
                    _downloadCancelTokens[v].Cancel();
                }
            }
        }

        private void InvokeLaunch(WPFDataTypes.Version v)
        {
            if (_hasLaunchTask)
                return;

            _hasLaunchTask = true;
            Task.Run(async () =>
            {
                try
                {
                    v.StateChangeInfo = new VersionStateChangeInfo(VersionState.MovingData);
                    if (!MoveMinecraftData(v.GamePackageFamily, v.PackageType))
                    {
                        Debug.WriteLine("Data restore error, aborting launch");
                        v.StateChangeInfo = null;
                        _hasLaunchTask = false;
                        Dispatcher.Invoke(() =>
                        {
                            ShowFriendlyError(Localization.Get("CouldntPrepareWorlds"),
                                Localization.Get("CouldntPrepareWorldsMessage"));
                        });
                        return;
                    }

                    v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Registering);
                    string gameDir = Path.GetFullPath(v.GameDirectory);
                    
                    // Bfix injection is now manual via unlock button
                    
                    try
                    {
                        await ReRegisterPackage(v.GamePackageFamily, gameDir, v);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("App re-register failed:\n" + e.ToString());
                        Dispatcher.Invoke(() =>
                        {
                            ShowFriendlyError(Localization.Get("CouldntRegister"),
                                Localization.Get("CouldntRegisterMessage"));
                        });
                        _hasLaunchTask = false;
                        v.StateChangeInfo = null;
                        return;
                    }

                    v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Launching);
                    try
                    {
                        if (v.PackageType == PackageType.GDK)
                        {
                            await Task.Run(() => {
                                string exePath = Path.Combine(gameDir, "Minecraft.Windows.exe");
                                ProcessStartInfo psi = new ProcessStartInfo
                                {
                                    FileName = exePath,
                                    WorkingDirectory = gameDir,
                                    UseShellExecute = false
                                };
                                Process.Start(psi);
                            });
                        }
                        else
                        {
                            var pkg = await Windows.System.AppDiagnosticInfo.RequestInfoForPackageAsync(v.GamePackageFamily);
                            if (pkg.Count > 0)
                            {
                                if (pkg.Count > 1)
                                {
                                    Debug.WriteLine("Multiple packages found");
                                }
                                var result = await pkg[0].LaunchAsync();
                                if (result.ExtendedError != null)
                                {
                                    throw result.ExtendedError;
                                }
                            }
                            else
                            {
                                throw new Exception("No packages found for package family " + v.GamePackageFamily);
                            }
                        }
                        Debug.WriteLine("App launch finished!");
                        
                        // Track this version as running
                        _runningVersion = v;
                        SaveRunningVersion(v);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("App launch failed:\n" + e.ToString());
                        Dispatcher.Invoke(() =>
                        {
                            ShowFriendlyError(Localization.Get("CouldntLaunch"),
                                Localization.Get("CouldntLaunchMessage"));
                        });
                    }
                }
                finally
                {
                    _hasLaunchTask = false;
                    v.StateChangeInfo = null;
                }
            });
        }
        
        // Save running version info to file
        private void SaveRunningVersion(WPFDataTypes.Version v)
        {
            try
            {
                var info = new
                {
                    VersionName = v.Name,
                    VersionUUID = v.UUID,
                    GameDirectory = v.GameDirectory,
                    PackageFamily = v.GamePackageFamily,
                    Timestamp = DateTime.Now
                };
                File.WriteAllText(RUNNING_VERSION_PATH, JsonConvert.SerializeObject(info, Formatting.Indented));
                Debug.WriteLine($"Saved running version: {v.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save running version: {ex.Message}");
            }
        }
        
        // Load running version info from file
        private WPFDataTypes.Version LoadRunningVersion()
        {
            try
            {
                if (!File.Exists(RUNNING_VERSION_PATH))
                    return null;
                
                var json = File.ReadAllText(RUNNING_VERSION_PATH);
                var info = JsonConvert.DeserializeObject<dynamic>(json);
                
                // Find the version in our list
                string versionName = info.VersionName;
                var version = _versions.FirstOrDefault(v => v.Name == versionName);
                
                if (version != null)
                {
                    Debug.WriteLine($"Loaded running version from file: {version.Name}");
                    return version;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load running version: {ex.Message}");
            }
            return null;
        }
        
        /// <summary>
        /// Query the actual installed Minecraft version from Windows AppxPackage system.
        /// This is more accurate than relying on launcher's registered version tracking.
        /// Returns the version string (e.g., "1.21.50.7") or null if not found.
        /// </summary>
        private string GetSystemInstalledMinecraftVersion(string packageFamily)
        {
            try
            {
                // Use PackageManager API to query installed packages
                var packageManager = new Windows.Management.Deployment.PackageManager();
                var packages = packageManager.FindPackages(packageFamily);
                
                foreach (var pkg in packages)
                {
                    try
                    {
                        // Get version from package identity
                        var pkgVersion = pkg.Id.Version;
                        string versionString = $"{pkgVersion.Major}.{pkgVersion.Minor}.{pkgVersion.Build}.{pkgVersion.Revision}";
                        Debug.WriteLine($"System installed Minecraft version detected: {versionString} (Package: {pkg.Id.FullName})");
                        return versionString;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to read package version: {ex.Message}");
                    }
                }
                
                // Fallback: Try PowerShell query
                Debug.WriteLine("PackageManager query returned no results, trying PowerShell fallback");
                return GetSystemInstalledMinecraftVersionViaPowerShell(packageFamily);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PackageManager query failed: {ex.Message}, trying PowerShell fallback");
                return GetSystemInstalledMinecraftVersionViaPowerShell(packageFamily);
            }
        }
        
        /// <summary>
        /// Fallback method using PowerShell to query Get-AppxPackage.
        /// More reliable in some edge cases where PackageManager API fails.
        /// </summary>
        private string GetSystemInstalledMinecraftVersionViaPowerShell(string packageFamily)
        {
            try
            {
                // Extract package name from family (e.g., "Microsoft.MinecraftUWP_8wekyb3d8bbwe" -> "Microsoft.MinecraftUWP")
                string packageName = packageFamily.Split('_')[0];
                
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Get-AppxPackage -Name '{packageName}' | Select-Object -ExpandProperty Version\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,  // CRITICAL: Must redirect stderr when UseShellExecute = false
                    UseShellExecute = false,
                    CreateNoWindow = true,  // CRITICAL: Hide the window completely
                    WindowStyle = ProcessWindowStyle.Hidden  // FIXED: Was Normal, causing visible window every 60s
                };
                
                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);
                    
                    if (!string.IsNullOrWhiteSpace(errors))
                    {
                        Debug.WriteLine($"PowerShell stderr: {errors}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        string versionString = output.Trim();
                        Debug.WriteLine($"PowerShell detected Minecraft version: {versionString}");
                        return versionString;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PowerShell version query failed: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Determine the best version to auto-select using multiple detection methods.
        /// Priority: 1) Currently running process, 2) System-installed version, 3) Registered version, 4) Most recent
        /// </summary>
        private WPFDataTypes.Version DetermineBestVersionToSelect(List<WPFDataTypes.Version> installedVersions, WPFDataTypes.Version registeredVersion)
        {
            if (installedVersions == null || installedVersions.Count == 0)
                return null;
            
            // Priority 1: Check if Minecraft is currently running
            var runningVersion = DetectRunningMinecraft();
            if (runningVersion != null && installedVersions.Contains(runningVersion))
            {
                Debug.WriteLine($"Auto-selecting currently running version: {runningVersion.Name}");
                return runningVersion;
            }
            
            // Priority 2: Query system for actually installed Minecraft version
            foreach (var version in installedVersions)
            {
                string systemVersion = GetSystemInstalledMinecraftVersion(version.GamePackageFamily);
                if (!string.IsNullOrEmpty(systemVersion))
                {
                    // Try to match system version to our installed versions
                    var matchedVersion = installedVersions.FirstOrDefault(v => v.Name == systemVersion);
                    if (matchedVersion != null)
                    {
                        Debug.WriteLine($"Auto-selecting system-installed version: {matchedVersion.Name}");
                        return matchedVersion;
                    }
                    
                    // If exact match fails, try fuzzy match (version numbers might differ slightly)
                    var parsedSystemVersion = ParseVersionNumber(systemVersion);
                    var closestMatch = installedVersions
                        .Select(v => new { Version = v, Parsed = ParseVersionNumber(v.Name) })
                        .Where(x => x.Parsed.Major == parsedSystemVersion.Major && 
                                   x.Parsed.Minor == parsedSystemVersion.Minor)
                        .OrderByDescending(x => x.Parsed)
                        .FirstOrDefault();
                    
                    if (closestMatch != null)
                    {
                        Debug.WriteLine($"Auto-selecting closest match to system version: {closestMatch.Version.Name} (system: {systemVersion})");
                        return closestMatch.Version;
                    }
                }
            }
            
            // Priority 3: Use registered version from launcher tracking
            if (registeredVersion != null && installedVersions.Contains(registeredVersion))
            {
                Debug.WriteLine($"Auto-selecting registered version: {registeredVersion.Name}");
                return registeredVersion;
            }
            
            // Priority 4: Select most recent version by version number
            var mostRecent = installedVersions
                .Select(v => new { Version = v, Parsed = ParseVersionNumber(v.Name) })
                .OrderByDescending(x => x.Parsed)
                .FirstOrDefault();
            
            if (mostRecent != null)
            {
                Debug.WriteLine($"Auto-selecting most recent version: {mostRecent.Version.Name}");
                return mostRecent.Version;
            }
            
            // Fallback: First in list
            Debug.WriteLine($"Auto-selecting first version in list: {installedVersions[0].Name}");
            return installedVersions[0];
        }
        
        // Detect which Minecraft version is currently running
        private WPFDataTypes.Version DetectRunningMinecraft()
        {
            try
            {
                // Get current process ID to exclude ourselves
                int currentProcessId = Process.GetCurrentProcess().Id;
                
                // Get ALL running processes
                var allProcesses = Process.GetProcesses();
                
                // Check if any process has "minecraft" in its name (case-insensitive)
                foreach (var proc in allProcesses)
                {
                    try
                    {
                        // Skip our own launcher process
                        if (proc.Id == currentProcessId)
                            continue;
                        
                        string processName = proc.ProcessName.ToLower();
                        
                        // Detect any minecraft-related process, but exclude launchers
                        if (processName.Contains("minecraft") && !processName.Contains("launcher"))
                        {
                            Debug.WriteLine($"Detected running Minecraft process: {proc.ProcessName}");
                            
                            // Try to match to an installed version
                            foreach (var version in _versions.Where(v => v.IsInstalled))
                            {
                                string gameDir = Path.GetFullPath(version.GameDirectory);
                                string minecraftExe = Path.Combine(gameDir, "Minecraft.Windows.exe");
                                
                                if (!File.Exists(minecraftExe))
                                    continue;
                                
                                try
                                {
                                    string procPath = proc.MainModule.FileName;
                                    if (procPath.Equals(minecraftExe, StringComparison.OrdinalIgnoreCase))
                                    {
                                        Debug.WriteLine($"Matched to installed version: {version.Name}");
                                        return version;
                                    }
                                }
                                catch { }
                            }
                            
                            // Minecraft is running but not from our launcher - return first installed version as marker
                            Debug.WriteLine("Minecraft is running (external instance)");
                            var firstVersion = _versions.FirstOrDefault(v => v.IsInstalled);
                            return firstVersion ?? _versions.FirstOrDefault();
                        }
                    }
                    catch { }
                }
                
                Debug.WriteLine("No Minecraft processes running");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error detecting running Minecraft: {ex.Message}");
            }
            return null;
        }
        
        // Kill all Minecraft processes
        private void KillAllMinecraft()
        {
            try
            {
                var processes = Process.GetProcessesByName("Minecraft.Windows");
                foreach (var proc in processes)
                {
                    try
                    {
                        Debug.WriteLine($"Killing Minecraft process PID {proc.Id}");
                        proc.Kill();
                        proc.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to kill process: {ex.Message}");
                    }
                }
                
                if (processes.Length > 0)
                {
                    Debug.WriteLine($"Killed {processes.Length} Minecraft process(es)");
                    Thread.Sleep(1000); // Wait for cleanup
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error killing Minecraft: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Lightweight timer callback that monitors Minecraft process state.
        /// Only performs expensive version queries when state changes (start/stop).
        /// Performance: ~1-3ms per tick (process enumeration only).
        /// </summary>
        private void VersionMonitorTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Fast check: Is Minecraft.Windows process running? (~1-3ms)
                bool isRunning = Process.GetProcessesByName("Minecraft.Windows").Length > 0;
                
                // State change detection
                if (isRunning != _wasMinecraftRunning)
                {
                    _wasMinecraftRunning = isRunning;
                    
                    if (isRunning)
                    {
                        Debug.WriteLine("[Monitor] Minecraft started - querying system version");
                        
                        // Minecraft just started - query system version (expensive, but only on state change)
                        Task.Run(() =>
                        {
                            try
                            {
                                // Check both UWP and Preview package families
                                string uwpVersion = GetSystemInstalledMinecraftVersion(MinecraftPackageFamilies.MINECRAFT);
                                string previewVersion = GetSystemInstalledMinecraftVersion(MinecraftPackageFamilies.MINECRAFT_PREVIEW);
                                
                                string detectedVersion = uwpVersion ?? previewVersion;
                                
                                if (!string.IsNullOrEmpty(detectedVersion))
                                {
                                    _cachedSystemVersion = detectedVersion;
                                    _lastSystemVersionCheck = DateTime.Now;
                                    Debug.WriteLine($"[Monitor] System version cached: {detectedVersion}");
                                    
                                    // Update _runningVersion if we can match it
                                    var matchedVersion = _versions.FirstOrDefault(v => v.Name == detectedVersion);
                                    if (matchedVersion != null)
                                    {
                                        _runningVersion = matchedVersion;
                                        SaveRunningVersion(matchedVersion);
                                        Debug.WriteLine($"[Monitor] Updated running version to: {matchedVersion.Name}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Monitor] Version query failed: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        Debug.WriteLine("[Monitor] Minecraft stopped");
                        // Minecraft stopped - clear running state but keep cached version
                        _runningVersion = null;
                    }
                }
                
                // Periodic cache refresh (every 60 seconds while running) to catch version updates
                if (isRunning && (DateTime.Now - _lastSystemVersionCheck).TotalSeconds > 60)
                {
                    Debug.WriteLine("[Monitor] Periodic version refresh (60s elapsed)");
                    Task.Run(() =>
                    {
                        try
                        {
                            string uwpVersion = GetSystemInstalledMinecraftVersion(MinecraftPackageFamilies.MINECRAFT);
                            string previewVersion = GetSystemInstalledMinecraftVersion(MinecraftPackageFamilies.MINECRAFT_PREVIEW);
                            string detectedVersion = uwpVersion ?? previewVersion;
                            
                            if (!string.IsNullOrEmpty(detectedVersion) && detectedVersion != _cachedSystemVersion)
                            {
                                _cachedSystemVersion = detectedVersion;
                                _lastSystemVersionCheck = DateTime.Now;
                                Debug.WriteLine($"[Monitor] Version changed: {detectedVersion}");
                                
                                var matchedVersion = _versions.FirstOrDefault(v => v.Name == detectedVersion);
                                if (matchedVersion != null)
                                {
                                    _runningVersion = matchedVersion;
                                    SaveRunningVersion(matchedVersion);
                                }
                            }
                            else
                            {
                                _lastSystemVersionCheck = DateTime.Now;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Monitor] Periodic refresh failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Monitor] Timer tick error: {ex.Message}");
            }
        }
        
        // Wait for Minecraft to fully start by monitoring the launcher's state tracking
        private async Task<bool> WaitForMinecraftReady(WPFDataTypes.Version v, int timeoutSeconds = 30)
        {
            string gameDir = Path.GetFullPath(v.GameDirectory);
            string minecraftExe = Path.Combine(gameDir, "Minecraft.Windows.exe");
            
            var startTime = DateTime.Now;
            bool processFound = false;
            bool windowReady = false;
            
            // Phase 1: Wait for launcher state to complete (StateChangeInfo becomes null)
            while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
            {
                if (v.StateChangeInfo == null)
                {
                    // Launcher finished its work
                    break;
                }
                
                // Still in launch process (Initializing, MovingData, Registering, Launching, etc.)
                var state = v.StateChangeInfo.VersionState;
                Debug.WriteLine($"WaitForMinecraftReady: Launcher state = {state}");
                await Task.Delay(500);
            }
            
            if (v.StateChangeInfo != null)
            {
                Debug.WriteLine("WaitForMinecraftReady: Timeout waiting for launcher state to complete");
                return false;
            }
            
            // Phase 2: Wait for process to exist and be ready
            var phase2Start = DateTime.Now;
            while ((DateTime.Now - phase2Start).TotalSeconds < 15)
            {
                try
                {
                    var processes = Process.GetProcessesByName("Minecraft.Windows");
                    foreach (var proc in processes)
                    {
                        try
                        {
                            string procPath = proc.MainModule.FileName;
                            if (procPath.Equals(minecraftExe, StringComparison.OrdinalIgnoreCase))
                            {
                                processFound = true;
                                
                                // Check if window is ready (has title and is visible)
                                if (!string.IsNullOrEmpty(proc.MainWindowTitle) && 
                                    proc.MainWindowTitle.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    windowReady = true;
                                    Debug.WriteLine($"WaitForMinecraftReady: Window ready - Title: {proc.MainWindowTitle}");
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                    
                    if (windowReady)
                        break;
                    
                    if (processFound)
                        Debug.WriteLine("WaitForMinecraftReady: Process found but window not ready yet");
                    
                    await Task.Delay(500);
                }
                catch { }
            }
            
            if (!processFound)
            {
                Debug.WriteLine("WaitForMinecraftReady: Process never started");
                return false;
            }
            
            if (!windowReady)
            {
                Debug.WriteLine("WaitForMinecraftReady: Window not ready, using fallback delay");
                await Task.Delay(3000); // Fallback safety delay
            }
            
            // Extra safety delay to ensure MC is past packaging/registration
            await Task.Delay(1000);
            
            Debug.WriteLine("WaitForMinecraftReady: Success");
            return true;
        }

        private void InvokeRemove(WPFDataTypes.Version v)
        {
            var result = MessageBox.Show(
                Localization.Format("RemoveVersionMessage", v.DisplayName),
                Localization.Get("RemoveVersion"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Task.Run(async () =>
                {
                    var success = await RemoveVersion(v);
                    if (success)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // Removed annoying success popup - user can see version is gone
                            RefreshVersionLists();
                        });
                    }
                });
            }
        }

        private async Task<bool> RemoveVersion(WPFDataTypes.Version v)
        {
            try
            {
                v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Unregistering);
                Debug.WriteLine("Unregistering version " + v.DisplayName);
                
                try
                {
                    await UnregisterPackage(v.GamePackageFamily, v, skipBackup: false);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed unregistering package:\n" + e.ToString());
                    Dispatcher.Invoke(() =>
                    {
                        ShowFriendlyError(
                            Localization.Get("CouldntRemove"),
                            Localization.Get("CouldntRemoveMessage") + "\n\n" + e.Message);
                    });
                    return false;
                }
                
                Debug.WriteLine("Cleaning up game files for version " + v.DisplayName);
                v.StateChangeInfo = new VersionStateChangeInfo(VersionState.CleaningUp);
                
                try
                {
                    // Use the \\?\ prefix to support long paths
                    string fullPath = Path.GetFullPath(v.GameDirectory);
                    string longPath = @"\\?\" + fullPath;
                    
                    if (Directory.Exists(fullPath))
                    {
                        Directory.Delete(longPath, true);
                        Debug.WriteLine($"Deleted game directory: {fullPath}");
                    }
                    else
                    {
                        Debug.WriteLine($"Game directory doesn't exist: {fullPath}");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed deleting game directory:\n" + e.ToString());
                    Dispatcher.Invoke(() =>
                    {
                        ShowFriendlyError(
                            Localization.Get("CouldntRemove"),
                            Localization.Get("CouldntRemoveMessage") + "\n\n" + e.Message);
                    });
                    return false;
                }

                // Update version list
                if (v.IsImported)
                {
                    Dispatcher.Invoke(() => _versions.Remove(v));
                    Debug.WriteLine("Removed imported version " + v.DisplayName);
                }
                else
                {
                    v.UpdateInstallStatus();
                    Debug.WriteLine("Removed release version " + v.DisplayName);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Remove failed: " + ex.ToString());
                Dispatcher.Invoke(() =>
                {
                    ShowFriendlyError(Localization.Get("CouldntRemove"), 
                        Localization.Get("CouldntRemoveMessage") + "\n\n" + ex.Message);
                });
                return false;
            }
            finally
            {
                v.StateChangeInfo = null;
            }
        }

        private void InvokeDownload(WPFDataTypes.Version v)
        {
            // Allow resume if paused, otherwise block if already downloading
            if (v.IsStateChanging && v.StateChangeInfo?.IsPaused != true)
                return;

            CancellationTokenSource cancelSource = new CancellationTokenSource();
            _downloadCancelTokens[v] = cancelSource;
            
            v.IsNew = false;
            
            // If resuming, keep existing StateChangeInfo, otherwise create new
            bool isResuming = v.StateChangeInfo != null && v.StateChangeInfo.IsPaused;
            
            if (v.StateChangeInfo == null || !v.StateChangeInfo.IsPaused)
            {
                v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Initializing);
            }
            else
            {
                // Resuming - reset to initializing but keep progress info
                v.StateChangeInfo.IsPaused = false;
                v.StateChangeInfo.VersionState = VersionState.Initializing;
            }
            
            v.StateChangeInfo.CancelCommand = new RelayCommand((o) =>
            {
                Debug.WriteLine($"Cancel requested for {v.DisplayName}");
                cancelSource.Cancel();
                
                // Clean up state immediately
                v.StateChangeInfo = null;
                RemoveActiveDownload(v);
                
                // Delete partial file on cancel
                string versionsFolder = GetVersionsFolder();
                string fileName = (v.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + v.Name + (v.PackageType == PackageType.UWP ? ".Appx" : ".msixvc");
                string dlPath = Path.Combine(versionsFolder, fileName);
                try
                {
                    if (File.Exists(dlPath))
                    {
                        File.Delete(dlPath);
                        Debug.WriteLine($"Deleted partial download: {dlPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete partial file: {ex.Message}");
                }
            });
            
            // Add to active downloads (ensure it's there for both new and resumed downloads)
            AddActiveDownload(v);

            Debug.WriteLine("Download start for version: " + v.DisplayName);
            
            Task.Run(async () =>
            {
                try
                {
                    // Use AppData versions folder instead of current directory
                    string versionsFolder = GetVersionsFolder();
                    string fileName = (v.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + v.Name + (v.PackageType == PackageType.UWP ? ".Appx" : ".msixvc");
                    string dlPath = Path.Combine(versionsFolder, fileName);
                    
                    VersionDownloader downloader = _anonVersionDownloader;

                    VersionDownloader.DownloadProgress dlProgressHandler = (current, total) =>
                    {
                        if (v.StateChangeInfo.VersionState != VersionState.Downloading)
                        {
                            Debug.WriteLine("Actual download started");
                            v.StateChangeInfo.VersionState = VersionState.Downloading;
                            if (total.HasValue)
                                v.StateChangeInfo.MaxProgress = total.Value;
                        }
                        v.StateChangeInfo.Progress = current;
                        
                        // Update download progress
                        if (total.HasValue && total.Value > 0)
                        {
                            int percentage = (int)((double)current / total.Value * 100);
                        }
                    };

                    try
                    {
                        if (v.PackageType == PackageType.UWP)
                        {
                            await downloader.DownloadAppx(v.UUID, "1", dlPath, dlProgressHandler, cancelSource.Token);
                        }
                        else if (v.PackageType == PackageType.GDK)
                        {
                            if (!ShowGDKFirstUseWarning())
                            {
                                v.StateChangeInfo = null;
                                v.UpdateInstallStatus();
                                RemoveActiveDownload(v);
                                return;
                            }
                            await downloader.DownloadMsixvc(v.DownloadURLs, dlPath, dlProgressHandler, cancelSource.Token);
                        }
                        else
                        {
                            throw new Exception("Unknown package type");
                        }
                        Debug.WriteLine("Download complete");
                    }
                    catch (BadUpdateIdentityException)
                    {
                        Debug.WriteLine("Download failed due to failure to fetch download URL");
                        Dispatcher.Invoke(() =>
                        {
                            ShowFriendlyError(Localization.Get("DownloadFailed"),
                                Localization.Get("DownloadFailedMessage") +
                                (v.VersionType == VersionType.Beta ? "\n" + Localization.Get("BadUpdateIDBeta") : ""));
                        });
                        v.StateChangeInfo = null;
                        RemoveActiveDownload(v);
                        return;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Download failed:\n" + e.ToString());
                        
                        // Check if this was a pause operation (not a real error)
                        if (e is TaskCanceledException && v.StateChangeInfo?.IsPaused == true)
                        {
                            Debug.WriteLine("Download paused by user - keeping state");
                            // Don't clear StateChangeInfo, keep it paused
                            // Don't remove from active downloads either
                            return;
                        }
                        
                        if (!(e is TaskCanceledException))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                ShowFriendlyError(Localization.Get("DownloadFailed"),
                                    Localization.Get("DownloadFailedMessage") + "\n\nError: " + e.Message);
                            });
                        }
                        v.StateChangeInfo = null;
                        RemoveActiveDownload(v);
                        return;
                    }

                    // Extract the downloaded package
                    try
                    {
                        string dirPath = v.GameDirectory;
                        if (Directory.Exists(dirPath))
                            Directory.Delete(dirPath, true);
                        
                        if (v.PackageType == PackageType.UWP)
                        {
                            await ExtractAppx(dlPath, dirPath, v);
                        }
                        else if (v.PackageType == PackageType.GDK)
                        {
                            await ExtractMsixvc(dlPath, dirPath, v, isPreview: v.VersionType == VersionType.Preview);
                        }
                        else
                        {
                            throw new Exception("Unknown package type");
                        }
                        
                        if (UserPrefs.DeleteAppxAfterDownload)
                        {
                            Debug.WriteLine("Deleting package to reduce disk usage");
                            File.Delete(dlPath);
                        }
                        else
                        {
                            Debug.WriteLine("Not deleting package due to user preferences");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Extraction failed:\n" + e.ToString());
                        Dispatcher.Invoke(() =>
                        {
                            ShowFriendlyError(Localization.Get("ExtractionFailed"),
                                Localization.Get("ExtractionFailedMessage") + "\n\nError: " + e.Message);
                        });
                        v.StateChangeInfo = null;
                        RemoveActiveDownload(v);
                        return;
                    }

                    v.StateChangeInfo = null;
                    v.UpdateInstallStatus();
                    RemoveActiveDownload(v);

                    Dispatcher.Invoke(() =>
                    {
                        // Removed annoying success popup - user can see version is installed
                        RefreshVersionLists();
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unexpected error in download task: {ex}");
                    RemoveActiveDownload(v);
                    v.StateChangeInfo = null;
                }
            });
        }

        private async Task<bool> ExtractAppx(string filePath, string directory, WPFDataTypes.Version versionEntry)
        {
            // CRITICAL: Acquire semaphore to prevent concurrent extraction corruption
            await _extractionSemaphore.WaitAsync();
            try
            {
                versionEntry.StateChangeInfo = new VersionStateChangeInfo(VersionState.Extracting);
                try
                {
                    bool success = await Task.Run(() =>
                    {
                        // Use robust extractor with progress callback
                        return RobustZipExtractor.ExtractZipFile(filePath, directory, (current, total) =>
                        {
                            if (total > 0)
                            {
                                long progress = (long)((double)current / total * 100);
                                // CRITICAL: Use BeginInvoke to avoid blocking the extraction thread
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (versionEntry.StateChangeInfo != null)
                                    {
                                        versionEntry.StateChangeInfo.MaxProgress = 100;
                                        versionEntry.StateChangeInfo.Progress = progress;
                                    }
                                }));
                            }
                        });
                    });
                    
                    if (!success)
                    {
                        Debug.WriteLine("Extract failed: RobustZipExtractor returned false");
                        ShowFriendlyError(Localization.Get("ExtractionFailed"), 
                            Localization.Get("ExtractionFailedMessage"));
                        return false;
                    }
                    
                    // Clean up signature file
                    string sigPath = Path.Combine(directory, "AppxSignature.p7x");
                    if (File.Exists(sigPath)) 
                    {
                        try { File.Delete(sigPath); } catch { }
                    }
                    
                    // Bfix injection is now manual via unlock button

                    versionEntry.UpdateInstallStatus();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Extract failed: " + ex.ToString());
                    ShowFriendlyError(Localization.Get("ExtractionFailed"), 
                        Localization.Get("ExtractionFailedMessage") + $"\n\nError: {ex.Message}");
                    return false;
                }
                finally
                {
                    versionEntry.StateChangeInfo = null;
                }
            }
            finally
            {
                _extractionSemaphore.Release();
            }
        }

        private void RecursiveCopyDirectory(string from, string to, HashSet<string> skip)
        {
            Directory.CreateDirectory(to);
            foreach (var source in Directory.EnumerateFiles(from))
            {
                if (skip.Contains(source))
                {
                    continue;
                }
                string destination = Path.Combine(to, Path.GetFileName(source));
                Debug.WriteLine(source + " -> " + destination);
                File.Copy(source, destination);
            }
            foreach (var source in Directory.EnumerateDirectories(from))
            {
                string destination = Path.Combine(to, Path.GetFileName(source));
                RecursiveCopyDirectory(source, destination, skip);
            }
        }
        
        /// <summary>
        /// Convert long path with spaces to 8.3 short path format
        /// Example: "C:\Users\big snigga\Desktop\file.msixvc" -> "C:\Users\BIGSNI~1\Desktop\file.msixvc"
        /// </summary>
        private static string GetShortPath(string longPath)
        {
            try
            {
                // Buffer needs to be larger for long paths
                StringBuilder shortPath = new StringBuilder(500);
                uint result = GetShortPathName(longPath, shortPath, (uint)shortPath.Capacity);
                
                if (result > 0 && result < shortPath.Capacity)
                {
                    string converted = shortPath.ToString();
                    Debug.WriteLine($"[GetShortPath] Converted: {longPath} -> {converted}");
                    return converted;
                }
                
                Debug.WriteLine($"[GetShortPath] Conversion failed (result={result}), using original path");
                return longPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetShortPath] Exception: {ex.Message}");
                return longPath;
            }
        }

        private async Task<bool> ExtractMsixvc(string filePath, string directory, WPFDataTypes.Version versionEntry, bool isPreview)
        {
            // CRITICAL: Use semaphore for atomic check-and-acquire (prevents race condition)
            bool acquired = await _gdkExtractSemaphore.WaitAsync(0); // Non-blocking check
            if (!acquired)
            {
                ShowFriendlyError(
                    Localization.Get("ConcurrentInstall"),
                    Localization.Get("ConcurrentInstallMessage"));
                return false;
            }
            try
            {
                directory = Path.GetFullPath(directory);
                // XVC are encrypted containers, I don't currently know of any way to extract them to an arbitrary directory
                // For now we just stage the package in XboxGames, and then move the files to the launcher data directory

                versionEntry.StateChangeInfo = new VersionStateChangeInfo(VersionState.Staging);

                var packageManager = new Windows.Management.Deployment.PackageManager();

                //make sure XboxGames is cleared
                Debug.WriteLine("Clearing existing XboxGames Minecraft installation");
                try
                {
                    await UnregisterPackage(versionEntry.GamePackageFamily, versionEntry, skipBackup: false);
                }
                catch (Exception ex)
                {
                    ShowFriendlyError(
                        Localization.Get("ClearXboxGamesFailed"),
                        Localization.Format("ClearXboxGamesFailedMessage", ex.Message));
                    return false;
                }

                CreateCustomInstallStub();

                try
                {
                    await DeploymentProgressWrapper(packageManager.StagePackageAsync(new Uri(filePath), null), versionEntry);
                }
                catch (Exception ex)
                {
                    ShowFriendlyError(
                        Localization.Get("StagingFailed"),
                        Localization.Get("StagingFailedMessage") + "\n\n" +
                        (isPreview ? Localization.Get("EnsurePreviewInstalled") : Localization.Get("EnsureMinecraftInstalled")) + "\n\n" +
                        "Error: " + ex.Message);
                    return false;
                }

                string installPath = "";
                foreach (var pkg in new Windows.Management.Deployment.PackageManager().FindPackages(versionEntry.GamePackageFamily))
                {
                    if (installPath != "")
                    {
                        ShowFriendlyError(
                            Localization.Get("MultiplePackagesFound"),
                            Localization.Get("MultiplePackagesFoundMessage"));
                        return false;
                    }
                    installPath = pkg.InstalledLocation.Path;
                }
                Debug.WriteLine("Detected staging path: " + installPath);
                string resolvedPath = LinkResolver.Resolve(installPath);
                Debug.WriteLine("Symlink resolved as " + resolvedPath);
                installPath = resolvedPath;

                var exeSrcPath = Path.Combine(installPath, "Minecraft.Windows.exe");
                if (!Directory.Exists(installPath))
                {
                    ShowFriendlyError(
                        Localization.Get("InstallPathNotFound"),
                        Localization.Format("InstallPathNotFoundMessage", installPath));
                    return false;
                }
                if (!File.Exists(exeSrcPath))
                {
                    ShowFriendlyError(
                        Localization.Get("MinecraftExeNotFound"),
                        Localization.Format("MinecraftExeNotFoundMessage", exeSrcPath));
                    return false;
                }

                versionEntry.StateChangeInfo.VersionState = VersionState.Decrypting;

                var exeTmpDir = Path.GetFullPath(@"tmp");
                if (!Directory.Exists(exeTmpDir))
                {
                    try
                    {
                        Directory.CreateDirectory(exeTmpDir);
                    }
                    catch (IOException ex)
                    {
                        ShowFriendlyError(
                            Localization.Get("TmpDirCreateFailed"),
                            Localization.Format("TmpDirCreateFailedMessage", exeTmpDir, ex.Message));
                        return false;
                    }
                }
                var uuid = Guid.NewGuid().ToString();
                //Use a different tmp path to make sure we don't copy half-done files
                //UUID makes sure we don't copy the leftovers of a different, failed installation
                var exeTmpPath = Path.Combine(exeTmpDir, "Minecraft.Windows_" + uuid + ".exe");
                var exePartialTmpPath = exeTmpPath + ".tmp";

                var exeDstPath = Path.Combine(Path.GetFullPath(directory), "Minecraft.Windows.exe");

                string psScript = $"Copy-Item '{exeSrcPath.Replace("'", "''")}' '{exePartialTmpPath.Replace("'", "''")}' -Force; Move-Item '{exePartialTmpPath.Replace("'", "''")}' '{exeTmpPath.Replace("'", "''")}'";
                byte[] scriptBytes = System.Text.Encoding.Unicode.GetBytes(psScript);
                string encodedScript = Convert.ToBase64String(scriptBytes);

                var command = $@"Invoke-CommandInDesktopPackage `
                            -PackageFamilyName ""{versionEntry.GamePackageFamily}"" `
                            -App Game `
                            -Command ""powershell.exe"" `
                            -Args \""-EncodedCommand {encodedScript}\""";
                
                Debug.WriteLine("Decrypt command: " + command);

                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = command,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                Debug.WriteLine("Copying decrypted exe");
                try
                {
                    var process = Process.Start(processInfo);
                    // CRITICAL: Don't block UI thread - wait asynchronously
                    await Task.Run(() => process.WaitForExit());
                    Debug.WriteLine("Process output:" + process.StandardOutput.ReadToEnd());
                    Debug.WriteLine("Process errors:" + process.StandardError.ReadToEnd());
                }
                catch (Exception ex)
                {
                    ShowFriendlyError(
                        Localization.Get("PowerShellExeFailed"),
                        Localization.Format("PowerShellExeFailedMessage", ex.Message));
                    return false;
                }

                for (int i = 0; i < 300 && !File.Exists(exeTmpPath); i++)
                {
                    //Give it up to 30 seconds to copy the file
                    //We can't block on the outcome of Invoke-CommandInDesktopPackage, so we have to poll for the file
                    //TODO: What if the copy takes longer than that?
                    await Task.Delay(100);
                }

                if (!File.Exists(exeTmpPath))
                {
                    Debug.WriteLine("Src path: " + exeSrcPath);
                    Debug.WriteLine("Tmp path: " + exeTmpPath);
                    ShowFriendlyError(
                        Localization.Get("ExeDecryptFailed"),
                        Localization.Get("ExeDecryptFailedMessage") + "\n\n" +
                        (isPreview ? Localization.Get("EnsurePreviewInstalled") : Localization.Get("EnsureMinecraftInstalled")));
                    return false;
                }
                Debug.WriteLine("Minecraft executable decrypted successfully");

                versionEntry.StateChangeInfo.VersionState = VersionState.Moving;
                //TODO: this could fail if the launcher is on a different drive than C: ?
                try
                {
                    Debug.WriteLine("Moving staged files: " + installPath + " -> " + directory);
                    
                    // CRITICAL: Move/copy operations can take a long time - run on background thread
                    await Task.Run(() =>
                    {
                        if (Path.GetPathRoot(installPath) == Path.GetPathRoot(directory))
                        {
                            Debug.WriteLine("Destination for extraction is on the same drive as the installation location - moving files for speed");
                            Directory.Move(installPath, directory);
                        }
                        else
                        {
                            Debug.WriteLine("Destination for extraction is on a different drive than staged - copying files");
                            //Minecraft.Windows.exe can't be copied directly due to permissions
                            HashSet<string> skip = new HashSet<string>();
                            skip.Add(exeSrcPath);
                            RecursiveCopyDirectory(installPath, directory, skip);
                        }
                    });

                    Debug.WriteLine("Moving decrypted exe into place");
                    await Task.Run(() =>
                    {
                        File.Delete(exeDstPath);
                        File.Move(exeTmpPath, exeDstPath);
                    });
                }
                catch (Exception ex)
                {
                    ShowFriendlyError(
                        Localization.Get("MoveFilesFailed"),
                        Localization.Format("MoveFilesFailedMessage", ex.Message));
                    return false;
                }

                Debug.WriteLine("Cleaning up XboxGames");
                //we already created a backup earlier, so a new attempt would just get in the way
                await UnregisterPackage(versionEntry.GamePackageFamily, versionEntry, skipBackup: true);

                Debug.WriteLine("Done importing msixvc: " + filePath);
                return true;

            }
            finally
            {
                _gdkExtractSemaphore.Release();
            }
        }


        /// <summary>
        /// Writes a minimal no-op x64 PE to C:\Windows\System32\custominstallexec.exe
        /// so Windows Gaming Services doesn't pop a "cannot find" dialog when it tries
        /// to run custom install actions during MSIXVC staging/registration.
        /// Only writes if the file does not already exist.
        /// </summary>
        private void CreateCustomInstallStub()
        {
            const string stubPath = @"C:\Windows\System32\custominstallexec.exe";
            if (File.Exists(stubPath))
            {
                Debug.WriteLine("custominstallexec.exe already exists — skipping stub");
                return;
            }

            try
            {
                // Minimal valid x64 PE (1024 bytes) whose entry point is: xor eax,eax; ret
                // No imports, no resources. Returns 0 immediately and terminates.
                byte[] pe = new byte[0x400]; // zero-filled

                // --- DOS Header ---
                pe[0x00] = 0x4D; pe[0x01] = 0x5A;  // MZ
                pe[0x3C] = 0x40;                    // e_lfanew = offset of PE header

                // --- PE Signature ---
                pe[0x40] = 0x50; pe[0x41] = 0x45;  // "PE\0\0"

                // --- COFF Header ---
                pe[0x44] = 0x64; pe[0x45] = 0x86;  // Machine = AMD64 (0x8664 LE)
                pe[0x46] = 0x01;                    // NumberOfSections = 1
                pe[0x54] = 0xF0;                    // SizeOfOptionalHeader = 240
                pe[0x56] = 0x22;                    // Characteristics: EXEC | LARGE_ADDRESS_AWARE

                // --- Optional Header (PE32+) ---
                pe[0x58] = 0x0B; pe[0x59] = 0x02;  // Magic = PE32+
                pe[0x5C] = 0x00; pe[0x5D] = 0x02;  // SizeOfCode = 0x200
                pe[0x68] = 0x00; pe[0x69] = 0x10;  // AddressOfEntryPoint = 0x1000
                pe[0x6C] = 0x00; pe[0x6D] = 0x10;  // BaseOfCode = 0x1000
                // ImageBase = 0x0000000140000000
                pe[0x73] = 0x40; pe[0x74] = 0x01;
                // SectionAlignment = 0x1000
                pe[0x78] = 0x00; pe[0x79] = 0x10;
                // FileAlignment = 0x200
                pe[0x7C] = 0x00; pe[0x7D] = 0x02;
                pe[0x80] = 0x06;                    // MajorOSVersion = 6
                pe[0x88] = 0x06;                    // MajorSubsystemVersion = 6
                pe[0x91] = 0x20;                    // SizeOfImage = 0x2000
                pe[0x95] = 0x02;                    // SizeOfHeaders = 0x200
                pe[0x9C] = 0x03;                    // Subsystem = 3 (console)
                pe[0xA2] = 0x10;                    // SizeOfStackReserve = 0x100000
                pe[0xA8] = 0x00; pe[0xA9] = 0x10;  // SizeOfStackCommit = 0x1000
                pe[0xB2] = 0x10;                    // SizeOfHeapReserve = 0x100000
                pe[0xB8] = 0x00; pe[0xB9] = 0x10;  // SizeOfHeapCommit = 0x1000
                pe[0xC4] = 0x10;                    // NumberOfRvaAndSizes = 16
                // DataDirectory[0..15] all zero (no imports/exports/resources)

                // --- Section Header[0] ".text" at 0x148 ---
                pe[0x148] = (byte)'.'; pe[0x149] = (byte)'t';
                pe[0x14A] = (byte)'e'; pe[0x14B] = (byte)'x'; pe[0x14C] = (byte)'t';
                pe[0x150] = 0x03;                    // VirtualSize = 3 (code bytes)
                pe[0x154] = 0x00; pe[0x155] = 0x10; // VirtualAddress = 0x1000
                pe[0x158] = 0x00; pe[0x159] = 0x02; // SizeOfRawData = 0x200
                pe[0x15C] = 0x00; pe[0x15D] = 0x02; // PointerToRawData = 0x200
                // Characteristics = 0x60000020 (CODE | MEM_EXECUTE | MEM_READ)
                pe[0x16C] = 0x20; pe[0x16F] = 0x60;

                // --- Code at file offset 0x200 ---
                pe[0x200] = 0x31; pe[0x201] = 0xC0; // xor eax, eax
                pe[0x202] = 0xC3;                    // ret  (BaseProcessStart calls ExitProcess(eax))

                File.WriteAllBytes(stubPath, pe);
                Debug.WriteLine($"Created no-op custominstallexec.exe stub at {stubPath}");
            }
            catch (Exception ex)
            {
                // Non-fatal: if we can't write it, the dialog might still appear,
                // but the import will continue regardless.
                Debug.WriteLine($"Warning: could not create custominstallexec.exe stub: {ex.Message}");
            }
        }

        private void FixGDKManifest(string path)
        {
            XDocument doc = XDocument.Load(path);
            XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
            XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

            // Fix executable name if needed
            var apps = doc.Descendants(ns + "Application");
            foreach (var app in apps)
            {
                var executable = app.Attribute("Executable");
                if (executable != null && executable.Value == "GameLaunchHelper.exe")
                {
                    executable.Value = "Minecraft.Windows.exe";
                }
                
                // Remove Extensions from inside Application element (uap:Extensions contains protocols)
                var uapExtensions = app.Elements(uap + "Extensions").ToList();
                foreach (var ext in uapExtensions)
                {
                    Debug.WriteLine("Removing uap:Extensions element from Application");
                    ext.Remove();
                }
                
                // Also check for non-namespaced Extensions
                var appExtensions = app.Elements(ns + "Extensions").ToList();
                foreach (var ext in appExtensions)
                {
                    Debug.WriteLine("Removing Extensions element from Application");
                    ext.Remove();
                }
            }

            // Remove Extensions from Package root (if any)
            var rootExtensions = doc.Root.Elements(ns + "Extensions").ToList();
            foreach (var ext in rootExtensions)
            {
                Debug.WriteLine("Removing Extensions element from Package root");
                ext.Remove();
            }
            
            var rootUapExtensions = doc.Root.Elements(uap + "Extensions").ToList();
            foreach (var ext in rootUapExtensions)
            {
                Debug.WriteLine("Removing uap:Extensions element from Package root");
                ext.Remove();
            }

            // Remove customInstallActions capability
            var capabilities = doc.Descendants(ns + "Capabilities");
            var customInstall = capabilities
                .Elements(rescap + "Capability")
                .Where(c => c.Attribute("Name")?.Value == "customInstallActions")
                .ToList();
            foreach (var cap in customInstall)
            {
                Debug.WriteLine("Removing customInstallActions capability");
                cap.Remove();
            }

            doc.Save(path);
            Debug.WriteLine($"Fixed GDK manifest: {path}");
        }

        private async Task DeploymentProgressWrapper(Windows.Foundation.IAsyncOperationWithProgress<Windows.Management.Deployment.DeploymentResult, Windows.Management.Deployment.DeploymentProgress> t, WPFDataTypes.Version version)
        {
            TaskCompletionSource<int> src = new TaskCompletionSource<int>();
            t.Progress += (v, p) =>
            {
                Debug.WriteLine("Deployment progress: " + p.state + " " + p.percentage + "%");
                // CRITICAL: Update UI with progress to prevent frozen appearance
                if (version.StateChangeInfo != null) {
                    // Use BeginInvoke for non-blocking UI updates
                    Dispatcher.BeginInvoke(new Action(() => {
                        version.StateChangeInfo.Progress = (long)p.percentage;
                        version.StateChangeInfo.MaxProgress = 100;
                        Debug.WriteLine($"UI Updated: Progress={p.percentage}%, MaxProgress=100");
                    }));
                }
            };
            t.Completed += (v, p) =>
            {
                if (p == Windows.Foundation.AsyncStatus.Error)
                {
                    Debug.WriteLine("Deployment failed: " + v.GetResults().ErrorText + " (error code " + v.GetResults().ExtendedErrorCode.HResult + ")");
                    src.SetException(new Exception("Deployment failed: " + v.GetResults().ErrorText));
                }
                else
                {
                    Debug.WriteLine("Deployment done: " + p);
                    src.SetResult(1);
                }
            };
            await src.Task;
        }

        private string GetPackagePath(Windows.ApplicationModel.Package pkg)
        {
            try
            {
                return pkg.InstalledLocation.Path;
            }
            catch (FileNotFoundException)
            {
                return "";
            }
        }

        private async Task RemovePackage(Windows.ApplicationModel.Package pkg, string packageFamily, WPFDataTypes.Version version, bool skipBackup)
        {
            Debug.WriteLine("Removing package: " + pkg.Id.FullName);
            if (!pkg.IsDevelopmentMode)
            {
                if (!skipBackup)
                {
                    if (!BackupMinecraftDataForRemoval(packageFamily))
                    {
                        throw new Exception("Failed backing up Minecraft data before uninstalling package");
                    }
                }
                await DeploymentProgressWrapper(new Windows.Management.Deployment.PackageManager().RemovePackageAsync(pkg.Id.FullName, Windows.Management.Deployment.RemovalOptions.RemoveForAllUsers), version);
            }
            else
            {
                Debug.WriteLine("Package is in development mode");
                await DeploymentProgressWrapper(new Windows.Management.Deployment.PackageManager().RemovePackageAsync(pkg.Id.FullName, Windows.Management.Deployment.RemovalOptions.PreserveApplicationData | Windows.Management.Deployment.RemovalOptions.RemoveForAllUsers), version);
            }
            Debug.WriteLine("Removal of package done: " + pkg.Id.FullName);
        }

        private bool BackupMinecraftDataForRemoval(string packageFamily)
        {
            Windows.Storage.ApplicationData data;
            try
            {
                data = Windows.Management.Core.ApplicationDataManager.CreateForPackageFamily(packageFamily);
            }
            catch (FileNotFoundException e)
            {
                Debug.WriteLine("BackupMinecraftDataForRemoval: Application data not found for package family " + packageFamily + ": " + e.ToString());
                Debug.WriteLine("This should mean the package isn't installed, so we don't need to backup the data");
                return true;
            }
            if (!Directory.Exists(data.LocalFolder.Path))
            {
                Debug.WriteLine("LocalState folder " + data.LocalFolder.Path + " doesn't exist, so it can't be backed up");
                return true;
            }
            string tmpDir = GetBackupMinecraftDataDir();
            if (Directory.Exists(tmpDir))
            {
                if (GetWorldCountInDataDir(tmpDir) > 0)
                {
                    // Previous backup exists with worlds — try to merge instead of blocking
                    Debug.WriteLine("BackupMinecraftDataForRemoval: previous backup exists with worlds at " + tmpDir);
                    Debug.WriteLine("Attempting to merge new data alongside existing backup");
                    
                    try
                    {
                        // Merge: move new files into existing backup dir without overwriting
                        RestoreMove(data.LocalFolder.Path, tmpDir);
                        
                        // Notify user that merge happened (non-blocking)
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                Localization.Get("BackupDirConflictMessage"),
                                Localization.Get("BackupDirConflict"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        });
                        return true;
                    }
                    catch (Exception mergeEx)
                    {
                        // Merge failed — fall back to showing error with Explorer
                        Debug.WriteLine("Merge failed: " + mergeEx.Message);
                        Process.Start("explorer.exe", tmpDir);
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                Localization.Get("BackupMergeFailedMessage"),
                                Localization.Get("BackupMergeFailed"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                        return false;
                    }
                }
                Directory.Delete(tmpDir, recursive: true);
            }
            Debug.WriteLine("Moving Minecraft data to: " + tmpDir);
            Directory.Move(data.LocalFolder.Path, tmpDir);

            return true;
        }

        private int GetWorldCountInDataDir(string dataDir)
        {
            var worldsFolder = Path.Combine(dataDir, "games", "com.mojang", "minecraftWorlds");
            if (!Directory.Exists(worldsFolder))
            {
                return 0;
            }
            return Directory.GetDirectories(worldsFolder).Length;
        }

        private async Task UnregisterPackage(string packageFamily, WPFDataTypes.Version version, bool skipBackup)
        {
            foreach (var pkg in new Windows.Management.Deployment.PackageManager().FindPackages(packageFamily))
            {
                string location = GetPackagePath(pkg);
                Debug.WriteLine("Removing package: " + pkg.Id.FullName + " " + location);
                await RemovePackage(pkg, packageFamily, version, skipBackup);
            }
        }

        private async Task ReRegisterPackage(string packageFamily, string gameDir, WPFDataTypes.Version version)
        {
            foreach (var pkg in new Windows.Management.Deployment.PackageManager().FindPackages(packageFamily))
            {
                string location = GetPackagePath(pkg);
                if (location == gameDir)
                {
                    Debug.WriteLine("Skipping package removal - same path: " + pkg.Id.FullName + " " + location);
                    return;
                }
                await RemovePackage(pkg, packageFamily, version, skipBackup: false);
            }
            Debug.WriteLine("Registering package");
            string manifestPath = Path.Combine(gameDir, "AppxManifest.xml");

            if (version.PackageType == PackageType.GDK)
            {
                string originalPath = Path.Combine(gameDir, "AppxManifest_original.xml");
                if (!File.Exists(originalPath))
                {
                    File.Copy(manifestPath, originalPath);
                    FixGDKManifest(manifestPath);
                }
            }
            Debug.WriteLine("Manifest path: " + manifestPath);
            await DeploymentProgressWrapper(new Windows.Management.Deployment.PackageManager().RegisterPackageAsync(new Uri(manifestPath), null, Windows.Management.Deployment.DeploymentOptions.DevelopmentMode), version);
            Debug.WriteLine("App re-register done!");
        }

        // Helper methods for launch functionality
        private bool MoveMinecraftData(string packageFamily, PackageType destinationType)
        {
            var dataLocations = LocateMinecraftWorlds(packageFamily);
            if (dataLocations.Count == 0)
            {
                Debug.WriteLine("No Minecraft data found to restore or link");
                return true;
            }

            string gdkRoot = GetMinecraftGDKRootDir(packageFamily);
            string uwpDataDir = GetMinecraftUWPDataDir(packageFamily);

            if (dataLocations.Count > 1)
            {
                var messageString = "";
                foreach (var loc in dataLocations)
                {
                    messageString += $"\n - {loc.Key}: {loc.Value} worlds";
                }

                if (destinationType == PackageType.GDK)
                {
                    bool gdkOnly = true;
                    foreach (var loc in dataLocations)
                    {
                        if (!loc.Key.StartsWith(gdkRoot))
                        {
                            gdkOnly = false;
                            break;
                        }
                    }

                    if (gdkOnly)
                    {
                        Debug.WriteLine("Worlds found in multiple GDK locations, this is fine");
                        return true;
                    }
                }

                Debug.WriteLine("Multiple world locations found:" + messageString);
                string destinationFolder = destinationType == PackageType.UWP ? uwpDataDir : Path.Combine(gdkRoot, "Users");
                
                // Auto-accept multiple world locations - user doesn't need to see this
                Debug.WriteLine($"Auto-accepting multiple world locations. Destination: {destinationFolder}");
                return true;
            }

            string dataLocation = dataLocations.Keys.First();
            string tmpDir = GetBackupMinecraftDataDir();
            string uwpParent = GetMinecraftUWPRootDir(packageFamily);

            if (dataLocation == tmpDir)
            {
                Debug.WriteLine("Restoring from backup");
                if (!RestoreUWPData(tmpDir, uwpDataDir, uwpParent))
                {
                    return false;
                }
                dataLocation = uwpDataDir;
            }

            if (destinationType == PackageType.GDK && dataLocation == uwpDataDir)
            {
                Debug.WriteLine("Preparing GDK migration from UWP");
                var uwpMigrationDat = Path.Combine(gdkRoot, "games", "com.mojang", "uwpMigration.dat");
                try
                {
                    if (File.Exists(uwpMigrationDat))
                    {
                        File.Delete(uwpMigrationDat);
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed deleting uwpMigration.dat: " + e.ToString());
                    return true; // Continue anyway
                }
            }
            else if (destinationType == PackageType.UWP && dataLocation != uwpDataDir)
            {
                var gdkDataDir = dataLocations.Keys.First();
                if (!RestoreUWPData(gdkDataDir, uwpDataDir, uwpParent))
                {
                    return false;
                }
                return true;
            }

            Debug.WriteLine("Data already in correct location");
            return true;
        }

        private Dictionary<string, int> LocateMinecraftWorlds(string packageFamily)
        {
            List<string> candidates = new List<string>();

            var uwpDataDir = GetMinecraftUWPDataDir(packageFamily);
            if (uwpDataDir != "")
            {
                candidates.Add(uwpDataDir);
            }

            candidates.AddRange(GetMinecraftGDKDataDirs(packageFamily));
            candidates.Add(GetBackupMinecraftDataDir());

            var worldLocations = new Dictionary<string, int>();
            foreach (var dataDir in candidates)
            {
                var worldsFolder = Path.Combine(dataDir, "games", "com.mojang", "minecraftWorlds");
                if (!Directory.Exists(worldsFolder))
                {
                    continue;
                }
                int worlds = Directory.GetDirectories(worldsFolder).Length;
                if (worlds > 0)
                {
                    worldLocations[dataDir] = worlds;
                }
            }

            return worldLocations;
        }

        private string GetBackupMinecraftDataDir()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "TmpMinecraftLocalState");
        }

        private string GetMinecraftUWPRootDir(string packageFamily)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                packageFamily);
        }

        private string GetMinecraftUWPDataDir(string packageFamily)
        {
            return Path.Combine(GetMinecraftUWPRootDir(packageFamily), "LocalState");
        }

        private string GetMinecraftGDKRootDir(string packageFamily)
        {
            string infix;
            switch (packageFamily)
            {
                case MinecraftPackageFamilies.MINECRAFT:
                    infix = "Minecraft Bedrock";
                    break;
                case MinecraftPackageFamilies.MINECRAFT_PREVIEW:
                    infix = "Minecraft Bedrock Preview";
                    break;
                default:
                    infix = "Minecraft Bedrock";
                    break;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), infix);
        }

        private List<string> GetMinecraftGDKDataDirs(string packageFamily)
        {
            var parentDir = Path.Combine(GetMinecraftGDKRootDir(packageFamily), "Users");
            var results = new List<string>();

            if (!Directory.Exists(parentDir))
            {
                return results;
            }

            results.AddRange(Directory.EnumerateDirectories(parentDir));
            return results;
        }

        private bool RestoreUWPData(string src, string uwpDataDir, string uwpParent)
        {
            try
            {
                if (!Directory.Exists(uwpParent))
                {
                    Directory.CreateDirectory(uwpParent);
                }
                if (!Directory.Exists(uwpDataDir))
                {
                    Directory.CreateDirectory(uwpDataDir);
                }

                RestoreMove(src, uwpDataDir);
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed restoring UWP data: " + e.ToString());
                return false;
            }
        }

        private void RestoreMove(string from, string to)
        {
            foreach (var f in Directory.EnumerateFiles(from))
            {
                string ft = Path.Combine(to, Path.GetFileName(f));
                if (File.Exists(ft))
                {
                    File.Delete(ft);
                }
                File.Move(f, ft);
            }
            foreach (var f in Directory.EnumerateDirectories(from))
            {
                string tp = Path.Combine(to, Path.GetFileName(f));
                if (!Directory.Exists(tp))
                {
                    Directory.CreateDirectory(tp);
                }
                RestoreMove(f, tp);
            }
        }

        private void ApplyCustomColors()
        {
            try
            {
                // Apply custom colors from preferences if they're set
                if (!string.IsNullOrEmpty(UserPrefs.Color_DarkBg))
                {
                    var darkBg = (SolidColorBrush)Resources["DarkBg"];
                    darkBg.Color = (Color)ColorConverter.ConvertFromString(UserPrefs.Color_DarkBg);
                }

                if (!string.IsNullOrEmpty(UserPrefs.Color_CardBg))
                {
                    var cardBg = (SolidColorBrush)Resources["CardBg"];
                    cardBg.Color = (Color)ColorConverter.ConvertFromString(UserPrefs.Color_CardBg);
                    
                    var cardHover = (SolidColorBrush)Resources["CardHover"];
                    // Make hover slightly lighter
                    var baseColor = (Color)ColorConverter.ConvertFromString(UserPrefs.Color_CardBg);
                    cardHover.Color = Color.FromRgb(
                        (byte)Math.Min(255, baseColor.R + 15),
                        (byte)Math.Min(255, baseColor.G + 15),
                        (byte)Math.Min(255, baseColor.B + 15)
                    );
                }

                if (!string.IsNullOrEmpty(UserPrefs.Color_AccentGreen))
                {
                    var accentGreen = (SolidColorBrush)Resources["AccentGreen"];
                    accentGreen.Color = (Color)ColorConverter.ConvertFromString(UserPrefs.Color_AccentGreen);
                    
                    var border = (SolidColorBrush)Resources["Border"];
                    border.Color = (Color)ColorConverter.ConvertFromString(UserPrefs.Color_AccentGreen);
                }

                if (!string.IsNullOrEmpty(UserPrefs.Color_TextPrimary))
                {
                    var textPrimary = (SolidColorBrush)Resources["TextPrimary"];
                    textPrimary.Color = (Color)ColorConverter.ConvertFromString(UserPrefs.Color_TextPrimary);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply custom colors: {ex.Message}");
                // Silently fail - use default colors
            }
        }
    }

    // ViewModel for modern UI
    public class ModernVersionViewModel : INotifyPropertyChanged
    {
        public WPFDataTypes.Version Version { get; }
        private ICommonVersionCommands _commands;
        private bool _isRegistered;

        public bool IsRegistered
        {
            get => _isRegistered;
            set
            {
                if (_isRegistered != value)
                {
                    _isRegistered = value;
                    OnPropertyChanged("IsRegistered");
                    OnPropertyChanged("RegisteredIndicatorVisibility");
                }
            }
        }

        public Visibility RegisteredIndicatorVisibility => IsRegistered ? Visibility.Visible : Visibility.Collapsed;

        public ModernVersionViewModel(WPFDataTypes.Version version, ICommonVersionCommands commands)
        {
            Version = version;
            _commands = commands;
            
            // Subscribe to Version property changes to update progress
            Version.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "IsStateChanging" || e.PropertyName == "StateChangeInfo")
                {
                    OnPropertyChanged("ProgressVisibility");
                    OnPropertyChanged("DownloadButtonVisibility");
                    OnPropertyChanged("InstalledVisibility");
                    OnPropertyChanged("ProgressText");
                    OnPropertyChanged("MaxProgress");
                    OnPropertyChanged("CurrentProgress");
                    OnPropertyChanged("CancelCommand");
                    OnPropertyChanged("CancelButtonVisibility");
                    OnPropertyChanged("PauseResumeButtonVisibility");
                    OnPropertyChanged("PauseResumeButtonText");
                    OnPropertyChanged("UnlockButtonVisibility");
                    OnPropertyChanged("FreeTrialUnlockButtonVisibility");

                    // Re-subscribe to new StateChangeInfo if it changed
                    if (e.PropertyName == "StateChangeInfo" && Version.StateChangeInfo != null)
                    {
                        Version.StateChangeInfo.PropertyChanged += StateChangeInfo_PropertyChanged;
                    }
                }
            };
            
            // Subscribe to StateChangeInfo property changes
            if (Version.StateChangeInfo != null)
            {
                Version.StateChangeInfo.PropertyChanged += StateChangeInfo_PropertyChanged;
            }
        }
        
        private void StateChangeInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Progress" || e.PropertyName == "MaxProgress" || e.PropertyName == "DisplayStatus")
            {
                OnPropertyChanged("ProgressText");
                OnPropertyChanged("MaxProgress");
                OnPropertyChanged("CurrentProgress");
            }
            if (e.PropertyName == "IsPaused")
            {
                OnPropertyChanged("PauseResumeButtonText");
            }
            if (e.PropertyName == "VersionState")
            {
                // When VersionState changes, update button visibility
                OnPropertyChanged("CancelButtonVisibility");
                OnPropertyChanged("PauseResumeButtonVisibility");
            }
        }

        public string DisplayName => Version.DisplayName;
        
        public string FriendlyStatus
        {
            get
            {
                if (Version.IsStateChanging && Version.StateChangeInfo != null)
                {
                    return Version.StateChangeInfo.DisplayStatus;
                }
                return Version.IsInstalled ? Localization.Get("ReadyToPlay") : Localization.Get("NotInstalled");
            }
        }

        public string PackageTypeDisplay
        {
            get
            {
                return Version.PackageType == PackageType.GDK ? Localization.Get("TypeXboxGDK") : Localization.Get("TypeWindowsStore");
            }
        }

        public string VersionTypeDisplay
        {
            get
            {
                switch (Version.VersionType)
                {
                    case VersionType.Release:
                        return Localization.Get("StableRelease");
                    case VersionType.Preview:
                        return Localization.Get("PreviewVersion");
                    case VersionType.Beta:
                        return Localization.Get("BetaVersion");
                    case VersionType.Imported:
                        return Localization.Get("ImportedVersion");
                    default:
                        return "";
                }
            }
        }

        public string Icon
        {
            get
            {
                switch (Version.VersionType)
                {
                    case VersionType.Release:
                        return "⭐";
                    case VersionType.Preview:
                        return "✨";
                    case VersionType.Beta:
                        return "🧪";
                    case VersionType.Imported:
                        return "📦";
                    default:
                        return "🎮";
                }
            }
        }

        // Progress properties
        public string ProgressText
        {
            get
            {
                if (Version.IsStateChanging && Version.StateChangeInfo != null)
                {
                    return Version.StateChangeInfo.DisplayStatus;
                }
                return "";
            }
        }

        public long MaxProgress
        {
            get
            {
                if (Version.IsStateChanging && Version.StateChangeInfo != null)
                {
                    return Version.StateChangeInfo.MaxProgress;
                }
                return 100;
            }
        }

        public long CurrentProgress
        {
            get
            {
                if (Version.IsStateChanging && Version.StateChangeInfo != null)
                {
                    return Version.StateChangeInfo.Progress;
                }
                return 0;
            }
        }

        public ICommand CancelCommand
        {
            get
            {
                if (Version.IsStateChanging && Version.StateChangeInfo != null)
                {
                    return Version.StateChangeInfo.CancelCommand;
                }
                return null;
            }
        }

        public ICommand PauseResumeCommand => _commands.PauseResumeCommand;

        public Visibility IsNewVisibility => Version.IsNew ? Visibility.Visible : Visibility.Collapsed;
        public Visibility IsRecommendedVisibility => Version.VersionType == VersionType.Release && !Version.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DownloadButtonVisibility => !Version.IsInstalled && !Version.IsStateChanging ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ProgressVisibility => Version.IsStateChanging ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CancelButtonVisibility => Version.IsStateChanging && Version.StateChangeInfo?.VersionState == VersionState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PauseResumeButtonVisibility => Version.IsStateChanging && Version.StateChangeInfo?.VersionState == VersionState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InstalledVisibility => Version.IsInstalled && !Version.IsStateChanging ? Visibility.Visible : Visibility.Collapsed;

        public Visibility UnlockButtonVisibility => Version.PackageType == PackageType.GDK && Version.IsInstalled && !Version.IsStateChanging ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FreeTrialUnlockButtonVisibility => Version.PackageType == PackageType.UWP && Version.IsInstalled && !Version.IsStateChanging ? Visibility.Visible : Visibility.Collapsed;

        public ICommand LaunchCommand => _commands.LaunchCommand;
        public ICommand RemoveCommand => _commands.RemoveCommand;
        public ICommand DownloadCommand => _commands.DownloadCommand;
        public ICommand UnlockCommand => _commands.UnlockCommand;
        public ICommand FreeTrialUnlockCommand => _commands.FreeTrialUnlockCommand;

        // Localized button text
        public string PlayButtonText => Localization.Get("Play");
        public string DownloadButtonText => Localization.Get("Download");
        public string RemoveTooltipText => Localization.Get("RemoveTooltip");
        public string UnlockTooltipText => Localization.Get("UnlockTooltip");
        public string FreeTrialUnlockTooltipText => Localization.Get("TrialUnlockTooltip");
        public string PauseResumeButtonText
        {
            get
            {
                if (Version.StateChangeInfo == null)
                    return Localization.Get("Pause");
                return Version.StateChangeInfo.IsPaused ? Localization.Get("Resume") : Localization.Get("Pause");
            }
        }
        public string CancelButtonText => Localization.Get("Cancel");

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
