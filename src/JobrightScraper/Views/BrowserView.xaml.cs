using System.Windows;
using System.Windows.Controls;
using JobrightScraper.ViewModels;

namespace JobrightScraper.Views;

public partial class BrowserView : UserControl
{
    public BrowserView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (DataContext is BrowserViewModel viewModel)
        {
            await viewModel.AttachWebViewAsync(Browser);
        }
    }
}
