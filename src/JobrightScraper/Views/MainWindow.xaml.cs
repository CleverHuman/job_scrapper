using System.Windows;
using JobrightScraper.ViewModels;

namespace JobrightScraper.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
