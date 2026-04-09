using System.Windows;
using RadioReel.App.Infrastructure.Accessibility;
using RadioReel.App.UI.ViewModels;

namespace RadioReel.App.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += (_, _) =>
        {
            AccessibilityHelper.Initialize(AssertiveLiveRegion, PoliteLiveRegion);
        };
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
