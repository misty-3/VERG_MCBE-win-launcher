using System.Windows;
using System.Windows.Media;

namespace MCLauncher
{
    public partial class WelcomeWizard : Window
    {
        private int _currentPage = 1;

        public WelcomeWizard()
        {
            InitializeComponent();
            UpdatePage();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < 3)
            {
                _currentPage++;
                UpdatePage();
            }
            else
            {
                // Finish wizard
                DialogResult = true;
                Close();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePage();
            }
        }

        private void UpdatePage()
        {
            // Hide all pages
            Page1.Visibility = Visibility.Collapsed;
            Page2.Visibility = Visibility.Collapsed;
            Page3.Visibility = Visibility.Collapsed;

            // Show current page
            switch (_currentPage)
            {
                case 1:
                    Page1.Visibility = Visibility.Visible;
                    BackButton.Visibility = Visibility.Collapsed;
                    NextButton.Content = "Next →";
                    UpdateDots(true, false, false);
                    break;
                case 2:
                    Page2.Visibility = Visibility.Visible;
                    BackButton.Visibility = Visibility.Visible;
                    NextButton.Content = "Next →";
                    UpdateDots(false, true, false);
                    break;
                case 3:
                    Page3.Visibility = Visibility.Visible;
                    BackButton.Visibility = Visibility.Visible;
                    NextButton.Content = "Let's Go! 🚀";
                    UpdateDots(false, false, true);
                    break;
            }
        }

        private void UpdateDots(bool dot1, bool dot2, bool dot3)
        {
            var activeColor = (SolidColorBrush)FindResource("MinecraftGreen");
            var inactiveColor = new SolidColorBrush(Color.FromRgb(224, 224, 224));

            Dot1.Fill = dot1 ? activeColor : inactiveColor;
            Dot2.Fill = dot2 ? activeColor : inactiveColor;
            Dot3.Fill = dot3 ? activeColor : inactiveColor;
        }
    }
}
