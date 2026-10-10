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
                   value.Contains("invalid api key") || value.Contains("invalid key") || value.Contains("unauthorized") ||
                   value.Contains("token is invalid") || value.Contains("invalid token") ||
                   value.Contains("missing application key") || value.Contains("authentication") ||
                   value.Contains("access denied");
        }
    }

}