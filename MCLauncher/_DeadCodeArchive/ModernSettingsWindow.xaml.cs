using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;

namespace MCLauncher
{
    public partial class ModernSettingsWindow : Window
    {
        private Preferences _preferences;

        // Auto-scroll support
        private bool _isAutoScrolling = false;
        private Point _autoScrollStartPoint;
        private ScrollViewer _autoScrollViewer;
        private DispatcherTimer _autoScrollTimer;
        private Ellipse _autoScrollIndicator;

        public ModernSettingsWindow(Preferences preferences)
        {
            InitializeComponent();
            _preferences = preferences;

            // Load current settings
            ShowInstalledOnlyCheckbox.IsChecked = _preferences.ShowInstalledOnly;
            DeleteAfterInstallCheckbox.IsChecked = _preferences.DeleteAppxAfterDownload;
            ShowBetaTabCheckbox.IsChecked = _preferences.ShowLegacyBetaTab;

            // Initialize auto-scroll timer
            _autoScrollTimer = new DispatcherTimer();
            _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            _preferences.ShowInstalledOnly = ShowInstalledOnlyCheckbox.IsChecked ?? false;
            _preferences.DeleteAppxAfterDownload = DeleteAfterInstallCheckbox.IsChecked ?? true;
            _preferences.ShowLegacyBetaTab = ShowBetaTabCheckbox.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", Directory.GetCurrentDirectory());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLogFile_Click(object sender, RoutedEventArgs e)
        {
            // Log functionality removed
            MessageBox.Show("Log functionality has been removed.", 
                "Feature Removed", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetWarnings_Click(object sender, RoutedEventArgs e)
        {
            _preferences.HasPreviouslyUsedGDK = false;
            MessageBox.Show("First-time warnings have been reset. You'll see them again next time you use those features.", 
                "Warnings Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveAllVersions_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "⚠️ Are you REALLY sure?\n\n" +
                "This will remove ALL installed versions from the launcher.\n" +
                "Your worlds will be safe, but you'll need to download versions again.\n\n" +
                "This is usually only needed if something is broken.",
                "Remove All Versions?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var confirmResult = MessageBox.Show(
                    "Last chance! Click Yes to remove all versions.",
                    "Final Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    // TODO: Implement removal of all versions
                    MessageBox.Show(
                        "This feature will remove all versions when fully implemented.\n\n" +
                        "For now, you can manually delete the 'imported_versions' folder.",
                        "Not Yet Implemented",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }

        private void FixStoreIssues_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will try to fix issues with Microsoft Store installations.\n\n" +
                "What it does:\n" +
                "• Cleans up temporary files\n" +
                "• Resets package registrations\n" +
                "• Prepares for a fresh Store install\n\n" +
                "Continue?",
                "Fix Store Issues",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // TODO: Implement store cleanup
                MessageBox.Show(
                    "Store cleanup will be implemented here.\n\n" +
                    "For now, try:\n" +
                    "1. Uninstall Minecraft from the Store\n" +
                    "2. Restart your computer\n" +
                    "3. Reinstall from the Store",
                    "Manual Steps",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // Native Windows mouse scrolling support
        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            System.Windows.Controls.ScrollViewer scrollViewer = sender as System.Windows.Controls.ScrollViewer;
            if (scrollViewer != null)
            {
                double scrollAmount = e.Delta * 0.5; // Adjust multiplier for scroll speed
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollAmount);
                e.Handled = true;
            }
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
    }
}
