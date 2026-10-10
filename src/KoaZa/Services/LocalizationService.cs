using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KoaZa.Services
{
    public static class LocalizationService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa", "language.txt");
        public static string Language { get; private set; } = "fa";
        public static bool IsPersian { get { return Language == "fa"; } }

        private static readonly Dictionary<string, string> Persian = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Refresh list / نوسازی","نوسازی فهرست"},{"Import fixtures / دریافت بازی‌ها","دریافت مسابقات"},
            {"API Settings / تنظیمات API","تنظیمات API"},{"Historical-data match analysis • تحلیل آماری بر اساس نتایج تاریخی","تحلیل مسابقه بر پایه نتایج تاریخی"},
            {"Date / روز:","روز:"},{"Competition / لیگ:","رقابت:"},{"Upcoming fixtures / بازی‌های پیش رو","مسابقات پیش رو"},
            {"Date / ساعت","تاریخ / ساعت"},{"League / لیگ","رقابت"},{"Home / میزبان","میزبان"},{"Away / میهمان","میهمان"},
            {"Match analysis / تحلیل مسابقه","تحلیل مسابقه"},{"Select a match from the list / یک مسابقه انتخاب کن","یک مسابقه را انتخاب کن"},
            {"Market probabilities / احتمال بازارها","احتمال بازارها"},{"Probabilities are estimates, not guarantees. / احتمال‌ها تخمینی هستند، نه تضمین نتیجه.","احتمال‌ها تخمینی هستند، نه تضمین نتیجه."},
            {"KoaZa • Local SQLite • Historical Poisson baseline","KoaZa • پایگاه داده محلی • مدل پواسون تاریخی"},
            {"API Providers / مدیریت سرویس‌های داده","مدیریت سرویس‌های داده"},{"Add provider","افزودن سرویس"},{"Enable / disable","فعال / غیرفعال"},
            {"Remove selected","حذف مورد انتخابی"},{"Save securely","ذخیره امن"},{"Fetch & save fixtures","دریافت و ذخیره مسابقات"},
            {"Auto fallback (recommended)","انتخاب خودکار سرویس"},{"API-Football","API-SPORTS"},{"API-Football (API key required)","API-SPORTS (نیازمند کلید)"},
            {"OpenLigaDB (no key; German leagues)","OpenLigaDB (بدون کلید؛ لیگ‌های آلمان)"},
            {"No upcoming fixtures found in the local database. Use Import fixtures to fetch schedules. / مسابقه‌ای در پایگاه داده نیست؛ ابتدا دریافت مسابقات را اجرا کن.","مسابقه‌ای در پایگاه داده نیست؛ ابتدا مسابقات را دریافت کن."},
            {"Fixture list uses data already saved locally. Refresh after importing new schedules.","فهرست از داده‌های ذخیره‌شده استفاده می‌کند. پس از دریافت، نوسازی کن."},
            {"Select a match from the list / یک مسابقه انتخاب کن","یک مسابقه را انتخاب کن"},
            {"Probabilities will be calculated from saved historical results.","احتمال‌ها از نتایج تاریخی ذخیره‌شده محاسبه می‌شوند."},
            {"No enabled data providers are saved. Open API Settings and enable at least one provider.","هیچ سرویس فعالی ذخیره نشده است. از تنظیمات API یک سرویس را فعال کن."}
        };

        public static void Load()
        {
            try { if (File.Exists(SettingsPath)) { var v = File.ReadAllText(SettingsPath).Trim().ToLowerInvariant(); if (v == "fa" || v == "en") Language = v; } } catch { }
        }
        public static void SetLanguage(string language)
        {
            Language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fa";
            try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)); File.WriteAllText(SettingsPath, Language); } catch { }
        }
        public static string T(string english, string persian) { return IsPersian ? persian : english; }
        public static string Translate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string result;
            if (IsPersian && Persian.TryGetValue(value, out result)) return result;
            if (!IsPersian)
            {
                foreach (var pair in Persian) if (string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase)) return pair.Key;
            }
            return value;
        }
        public static void Apply(Window window)
        {
            window.FlowDirection = IsPersian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            ApplyTo(window);
        }
        private static void ApplyTo(DependencyObject parent)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                var tb = child as TextBlock; if (tb != null) tb.Text = Translate(tb.Text);
                var button = child as Button; if (button != null && button.Content is string) button.Content = Translate((string)button.Content);
                var label = child as Label; if (label != null && label.Content is string) label.Content = Translate((string)label.Content);
                var combo = child as ComboBox;
                if (combo != null)
                {
                    foreach (var item in combo.Items)
                    {
                        var text = item as string;
                        if (text != null)
                        {
                            int index = combo.Items.IndexOf(item);
                            combo.Items[index] = Translate(text);
                        }
                    }
                }
                ApplyTo(child);
            }
        }

        public static string Team(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !IsPersian) return name;
            string value = name.Trim();
            string known;
            if (Teams.TryGetValue(value, out known)) return known;
            // Preserve names already written in Persian/Arabic script.
            if (value.Any(c => c >= '\u0600' && c <= '\u06FF')) return value;
            return Transliterate(value);
        }
        private static readonly Dictionary<string, string> Teams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Arsenal","آرسنال"},{"Manchester City","منچسترسیتی"},{"Manchester United","منچستریونایتد"},{"Liverpool","لیورپول"},
            {"Chelsea","چلسی"},{"Tottenham Hotspur","تاتنهام"},{"Tottenham","تاتنهام"},{"Newcastle United","نیوکاسل"},
            {"Aston Villa","استون ویلا"},{"West Ham United","وستهم"},{"Brighton","برایتون"},{"Real Madrid","رئال مادرید"},
            {"Barcelona","بارسلونا"},{"Atletico Madrid","اتلتیکو مادرید"},{"Atlético Madrid","اتلتیکو مادرید"},{"Sevilla","سویا"},
            {"Bayern Munich","بایرن مونیخ"},{"Borussia Dortmund","بروسیا دورتموند"},{"Bayer Leverkusen","بایر لورکوزن"},
            {"RB Leipzig","لایپزیگ"},{"Inter","اینتر"},{"Inter Milan","اینتر میلان"},{"AC Milan","آث میلان"},{"Juventus","یوونتوس"},
            {"Napoli","ناپولی"},{"Roma","رم"},{"Lazio","لاتزیو"},{"Paris Saint-Germain","پاری‌سن‌ژرمن"},{"PSG","پاری‌سن‌ژرمن"},
            {"Marseille","مارسی"},{"Lyon","لیون"},{"Ajax","آژاکس"},{"PSV Eindhoven","پی‌اس‌وی آیندهوون"},{"Feyenoord","فاینورد"},
            {"Porto","پورتو"},{"Benfica","بنفیکا"},{"Sporting CP","اسپورتینگ"},{"Celtic","سلتیک"},{"Rangers","رنجرز"},
            {"Iran","ایران"},{"Iran National Team","تیم ملی ایران"},{"Persepolis","پرسپولیس"},{"Esteghlal","استقلال"},
            {"Sepahan","سپاهان"},{"Tractor","تراکتور"},{"Foolad","فولاد"},{"Al Hilal","الهلال"},{"Al Nassr","النصر"},
            {"Al-Ittihad","الاتحاد"},{"Japan","ژاپن"},{"South Korea","کره جنوبی"},{"Brazil","برزیل"},{"Argentina","آرژانتین"},
            {"France","فرانسه"},{"Germany","آلمان"},{"Spain","اسپانیا"},{"England","انگلیس"},{"Italy","ایتالیا"},{"Portugal","پرتغال"},
            {"Netherlands","هلند"},{"Belgium","بلژیک"},{"Croatia","کرواسی"},{"Turkey","ترکیه"},{"United States","آمریکا"}
        };
        private static string Transliterate(string s)
        {
            string v = s.ToLowerInvariant();
            string[,] pairs = {
                {"sh","ش"},{"ch","چ"},{"zh","ژ"},{"kh","خ"},{"gh","غ"},{"ph","ف"},{"th","ث"},{"oo","و"},{"ou","و"},{"ee","ی"},{"ea","ی"},{"ai","ای"},{"ay","ی"},{"ow","او"},
                {"a","ا"},{"b","ب"},{"c","ک"},{"d","د"},{"e","ی"},{"f","ف"},{"g","گ"},{"h","ه"},{"i","ی"},{"j","ج"},{"k","ک"},{"l","ل"},{"m","م"},{"n","ن"},{"o","و"},{"p","پ"},{"q","ق"},{"r","ر"},{"s","س"},{"t","ت"},{"u","و"},{"v","و"},{"w","و"},{"x","کس"},{"y","ی"},{"z","ز"}
            };
            for (int i = 0; i < pairs.GetLength(0); i++) v = v.Replace(pairs[i,0], pairs[i,1]);
            return v;
        }
    }
}
