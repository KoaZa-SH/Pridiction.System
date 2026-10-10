using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KoaZa.Services
{
    /// <summary>
    /// Stores API keys encrypted for the current Windows user. Keys are never written to source files.
    /// </summary>
    public static class ApiKeyStore
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa");
        private static readonly string FilePath = Path.Combine(Folder, "api-keys.dat");

        public static List<string> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<string>();
                byte[] protectedBytes = File.ReadAllBytes(FilePath);
                byte[] clearBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Parse(Encoding.UTF8.GetString(clearBytes));
            }
            catch
            {
                // A file protected by another Windows account cannot be read by this account.
                return new List<string>();
            }
        }

        public static void Save(IEnumerable<string> keys)
        {
            var normalized = Normalize(keys);
            Directory.CreateDirectory(Folder);
            byte[] clearBytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, normalized));
            byte[] protectedBytes = ProtectedData.Protect(clearBytes, null, DataProtectionScope.CurrentUser);
            string temp = FilePath + ".tmp";
            File.WriteAllBytes(temp, protectedBytes);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(temp, FilePath);
        }

        public static List<string> Parse(string text)
        {
            return Normalize((text ?? string.Empty).Split(new[] { '\r', '\n', ',', ';', '\t' },
                StringSplitOptions.RemoveEmptyEntries));
        }

        private static List<string> Normalize(IEnumerable<string> keys)
        {
            return keys.Select(k => (k ?? string.Empty).Trim())
                .Where(k => k.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>
    /// Tries configured keys in order. Rotates only for authentication/quota/rate-limit failures,
    /// rather than consuming every key for ordinary network/server errors.
    /// </summary>
    public static class ApiFootballClient
    {
        private const string BaseUrl = "https://v3.football.api-sports.io";
        private static int _nextKeyIndex;

        public static string Get(string pathAndQuery)
        {
            var keys = ApiKeyStore.Load();
            if (keys.Count == 0)
                throw new InvalidOperationException("No API keys are saved. Open API Settings and add at least one key.");

            string path = (pathAndQuery ?? string.Empty).Trim();
            if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;
            Exception lastError = null;

            for (int attempt = 0; attempt < keys.Count; attempt++)
            {
                int index = (_nextKeyIndex + attempt) % keys.Count;
                try
                {
                    string body = Send(keys[index], path);
                    if (IsQuotaOrAuthError(body))
                    {
                        lastError = new InvalidOperationException("This key was rejected or has reached a limit.");
                        continue;
                    }

                    _nextKeyIndex = (index + 1) % keys.Count;
                    return body;
                }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    int status = response == null ? 0 : (int)response.StatusCode;
                    string responseBody = ReadErrorBody(response);
                    if (status == 401 || status == 403 || status == 429 ||
                        IsQuotaOrAuthError(responseBody))
                    {
                        lastError = new InvalidOperationException(
                            "API key " + (index + 1) + " was rejected or rate-limited (HTTP " + status + ").");
                        continue;
                    }

                    // A timeout, offline connection, or server-side 5xx is not a reason to burn other keys.
                    throw new InvalidOperationException("API-Football connection failed. HTTP " + status +
                        ". Check internet access and try again.", ex);
                }
            }

            throw new InvalidOperationException("All saved API keys were rejected or rate-limited. " +
                "Check the keys and their quota in the API-Football account. " +
                (lastError == null ? string.Empty : lastError.Message));
        }

        public static string TestKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Enter an API key first.");
            string body = Send(key.Trim(), "/status");
            if (IsQuotaOrAuthError(body))
                throw new InvalidOperationException("The API returned an authentication or quota error.");
            if (body.IndexOf("\"errors\"", StringComparison.OrdinalIgnoreCase) >= 0 &&
                body.IndexOf("\"response\":[]", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("The API returned an error. Check the key and account plan.");
            return body;
        }

        private static string Send(string key, string path)
        {
            var request = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
            request.Method = "GET";
            request.Headers["x-apisports-key"] = key;
            request.Accept = "application/json";
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static string ReadErrorBody(HttpWebResponse response)
        {
            if (response == null) return string.Empty;
            try
            {
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch { return string.Empty; }
            finally { response.Close(); }
        }

        private static bool IsQuotaOrAuthError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            string value = body.ToLowerInvariant();
            return value.Contains("rate limit") || value.Contains("requests limit") ||
                   value.Contains("request limit") || value.Contains("quota") ||
                   value.Contains("too many requests") || value.Contains("limit reached") ||
                   value.Contains("invalid api key") || value.Contains("unauthorized") ||
                   value.Contains("token is invalid") || value.Contains("access denied");
        }
    }

    /// <summary>Small settings window, created in code to avoid additional XAML files.</summary>
    public sealed class ApiSettingsWindow : Window
    {
        private readonly TextBox _keysBox;
        private readonly TextBlock _status;
        private readonly Button _saveButton;
        private readonly Button _testButton;

        public ApiSettingsWindow()
        {
            Title = "KoaZa — API Settings";
            Width = 650;
            Height = 520;
            MinWidth = 520;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(244, 247, 245));

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "API-Football keys",
                FontSize = 23,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(22, 59, 43)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            root.Children.Add(title);
            Grid.SetRow(title, 0);

            var instructions = new TextBlock
            {
                Text = "Enter one key per line. Keys are encrypted for this Windows user and are not stored in the project or GitHub.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(instructions);
            Grid.SetRow(instructions, 1);

            _keysBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(8),
                Text = string.Join(Environment.NewLine, ApiKeyStore.Load())
            };
            root.Children.Add(_keysBox);
            Grid.SetRow(_keysBox, 2);

            _status = new TextBlock
            {
                Text = ApiKeyStore.Load().Count + " saved key(s).",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 12),
                Foreground = new SolidColorBrush(Color.FromRgb(65, 84, 73))
            };
            root.Children.Add(_status);
            Grid.SetRow(_status, 3);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _testButton = new Button { Content = "Test first key", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
            _testButton.Click += TestButton_Click;
            _saveButton = new Button
            {
                Content = "Save keys",
                Padding = new Thickness(18, 8, 18, 8),
                Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            _saveButton.Click += SaveButton_Click;
            buttons.Children.Add(_testButton);
            buttons.Children.Add(_saveButton);
            root.Children.Add(buttons);
            Grid.SetRow(buttons, 4);

            Content = root;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var keys = ApiKeyStore.Parse(_keysBox.Text);
                if (keys.Count == 0)
                {
                    MessageBox.Show(this, "Add at least one API key.", "API Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                ApiKeyStore.Save(keys);
                _keysBox.Text = string.Join(Environment.NewLine, keys);
                _status.Text = keys.Count + " key(s) saved securely. Automatic rotation is enabled for API auth/quota/rate-limit errors.";
                MessageBox.Show(this, "API keys saved on this computer.", "API Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save keys: " + ex.Message, "API Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var keys = ApiKeyStore.Parse(_keysBox.Text);
            if (keys.Count == 0)
            {
                _status.Text = "Enter at least one key before testing.";
                return;
            }

            _testButton.IsEnabled = false;
            _saveButton.IsEnabled = false;
            _status.Text = "Testing the first key against the API-Football /status endpoint...";
            try
            {
                string body = ApiFootballClient.TestKey(keys[0]);
                ApiKeyStore.Save(keys);
                _status.Text = "Connection successful. The first key received a response from API-Football. Saved " + keys.Count + " key(s).";
            }
            catch (Exception ex)
            {
                _status.Text = "Test failed: " + ex.Message;
            }
            finally
            {
                _testButton.IsEnabled = true;
                _saveButton.IsEnabled = true;
            }
        }
    }
}