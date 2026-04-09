using System.Windows;
using RadioReel.App.Core.Models;

namespace RadioReel.App.UI.Views.Dialogs;

public partial class AddStreamDialog : Window
{
    public StreamEntry? Result { get; private set; }

    public AddStreamDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => UrlTextBox.Focus();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var url = UrlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url))
        {
            MessageBox.Show("URL потоку є обов'язковим.",
                "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            UrlTextBox.Focus();
            return;
        }

        // Validate URL format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            MessageBox.Show("Введіть коректний HTTP/HTTPS URL.",
                "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            UrlTextBox.Focus();
            return;
        }

        Result = new StreamEntry
        {
            Url = url,
            Name = NameTextBox.Text.Trim()
        };

        DialogResult = true;
    }
}
