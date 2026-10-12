using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using KoaZa.Data;
using KoaZa.Services;

namespace KoaZa
{
    public sealed class DataSourcesWindow : Window
    {
        private readonly DatabaseService _database;
        private readonly TextBlock _status;
        private readonly ListBox _news;

        public DataSourcesWindow(DatabaseService database)
        {
            _database = database;
            Title = "Historical CSV & Persian RSS | KoaZa";
            Width = 850; Height = 650; MinWidth = 700; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            var root = new DockPanel { Margin = new Thickness(18) };
            var header = new StackPanel { Orientation = Orientation.Vertical };
            header.Children.Add(new TextBlock { Text = "Historical data & Persian news", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,6) });
            header.Children.Add(new TextBlock { Text = "CSV import updates SQLite history; RSS headlines are cached locally for offline reading.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,12) };
            var csv = new Button { Content = "Import historical CSV…", Margin = new Thickness(0,0,8,0), Padding = new Thickness(12,8,12,8) };
            csv.Click += ImportCsv_Click;
            var rss = new Button { Content = "Refresh Persian RSS", Padding = new Thickness(12,8) };
            rss.Click += RefreshRss_Click;
            buttons.Children.Add(csv); buttons.Children.Add(rss);
            header.Children.Add(buttons);
            DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10), Foreground = System.Windows.Media.Brushes.DarkSlateGray };
            DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
            _news = new ListBox();
            _news.DisplayMemberPath = "Title";
            root.Children.Add(_news);
            Content = root;
            Loaded += async (s,e) => await LoadCacheAsync();
        }

        private async void ImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "Select historical football CSV", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", Multiselect = false };
            if (dialog.ShowDialog(this) != true) return;
            _status.Text = "Importing and validating rows…";
            try
            {
                var result = await Task.Run(() => HistoricalCsvImporter.Import(dialog.FileName, _database));
                _status.Text = "CSV import complete. Imported/updated: " + result.Imported + "; skipped: " + result.Skipped +
                    (result.Warnings.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, result.Warnings.Take(5)) : "");
                MessageBox.Show(this, _status.Text, "CSV import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _status.Text = "CSV import failed: " + ex.Message;
                MessageBox.Show(this, _status.Text, "CSV import", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void RefreshRss_Click(object sender, RoutedEventArgs e) { await LoadCacheAsync(true); }
        private async Task LoadCacheAsync(bool refresh = false)
        {
            _status.Text = refresh ? "Fetching RSS feeds (cached headlines remain available on failure)…" : "Loading cached headlines…";
            try
            {
                var rows = await Task.Run(() => refresh ? RssNewsService.FetchAll() : RssNewsService.ReadCache());
                _news.ItemsSource = rows;
                _status.Text = rows.Count + " headlines from Persian sports RSS feeds. Cached locally for offline access.";
            }
            catch (Exception ex) { _status.Text = "RSS unavailable: " + ex.Message; }
        }
    }
}