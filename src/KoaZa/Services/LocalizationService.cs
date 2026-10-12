using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            {"Refresh list / نوسازی","نوسازی فهرست"},{"Language / زبان","زبان"},{"All next 7 days","تمام ۷ روز آینده"},{"All competitions","همه رقابت‌ها"},{"Import fixtures / دریافت بازی‌ها","دریافت مسابقات"},
            {"API Settings / تنظیمات API","تنظیمات API"},{"Settings / تنظیمات","تنظیمات"},{"General settings","تنظیمات عمومی"},{"Appearance","ظاهر برنامه"},{"Choose how the application should look.","حالت نمایش برنامه را انتخاب کن."},{"Follow Windows","مطابق ویندوز"},{"Light","روشن"},{"Dark","تاریک"},{"Database safety","امنیت پایگاه داده"},{"Create backup now","تهیهٔ پشتیبان اکنون"},{"Automatic backups run daily and before importing refreshed fixtures. The 30 newest backups are retained. Completed results replaced by a provider update are archived in the database history table.","پشتیبان‌گیری خودکار روزانه و پیش از دریافت مجدد مسابقات انجام می‌شود. ۳۰ نسخهٔ جدید نگه‌داری می‌شود. نتایج تمام‌شده‌ای که سرویس داده به‌روزرسانی کند، در جدول آرشیو ذخیره می‌شوند."},{"Backup created: ","پشتیبان ساخته شد: "},{"Backup failed: ","تهیه پشتیبان ناموفق بود: "},{"Total over 8.5","مجموع بیشتر از ۸٫۵"},{"Total under 8.5","مجموع کمتر از ۸٫۵"},{"Total over 3.5","مجموع بیشتر از ۳٫۵"},{"Total under 3.5","مجموع کمتر از ۳٫۵"},{"Total over 22.5","مجموع بیشتر از ۲۲٫۵"},{"Total under 22.5","مجموع کمتر از ۲۲٫۵"},{"Total over 0.5","مجموع بیشتر از ۰٫۵"},{"Total under 0.5","مجموع کمتر از ۰٫۵"},{"Corners","کرنرها"},{"Yellow cards","کارت زرد"},{"Not calculated: historical corner/card statistics are unavailable.","محاسبه نشده: آمار تاریخی کرنر و کارت در دسترس نیست."},{"Stats unavailable","آمار موجود نیست"},{"partial data","داده ناقص"},{"Historical-data match analysis • تحلیل آماری بر اساس نتایج تاریخی","تحلیل مسابقه بر پایه نتایج تاریخی"},
            {"Date / روز:","روز:"},{"Competition / لیگ:","رقابت:"},{"Upcoming fixtures / بازی‌های پیش رو","مسابقات پیش رو"},
            {"Date / ساعت","تاریخ / ساعت"},{"League / لیگ","رقابت"},{"Home / میزبان","میزبان"},{"Away / میهمان","میهمان"},
            {"Match analysis / تحلیل مسابقه","تحلیل مسابقه"},{"Select a match from the list / یک مسابقه انتخاب کن","یک مسابقه را انتخاب کن"},
            {"Market probabilities / احتمال بازارها","احتمال بازارها"},{"Home team / میزبان","تیم میزبان"},{"Away team / میهمان","تیم میهمان"},{"Probabilities are estimates, not guarantees. / احتمال‌ها تخمینی هستند، نه تضمین نتیجه.","احتمال‌ها تخمینی هستند، نه تضمین نتیجه."},
            {"KoaZa • Local SQLite • Historical Poisson baseline","KoaZa • پایگاه داده محلی • مدل پواسون تاریخی"},
            {"Additional markets use real historical corners, shots, cards, referee and VAR data when enough samples are available; missing values are never assumed to be zero.","بازارهای آماری از داده واقعی کرنر، شوت، کارت، داور و VAR استفاده می‌کنند؛ داده ناموجود صفر فرض نمی‌شود."},
            {"Match statistics & events / آمار و رویدادهای مسابقه","آمار و رویدادهای مسابقه"},
            {"Relevant match news / اخبار مرتبط با مسابقه","اخبار مرتبط با مسابقه"},
            {"Select a match to load statistics, cards, fouls, corners and events.","برای دیدن آمار، کارت‌ها، خطاها، کرنرها و رویدادها یک مسابقه انتخاب کن."},
            {"News will be searched when a match is selected.","پس از انتخاب مسابقه، اخبار مرتبط جست‌وجو می‌شود."},
            {"VS","در برابر"},
            {"API Providers / مدیریت سرویس‌های داده","مدیریت سرویس‌های داده"},{"Add provider","افزودن سرویس"},{"Enable / disable","فعال / غیرفعال"},
            {"Remove selected","حذف مورد انتخابی"},{"Save securely","ذخیره امن"},{"Fetch & save fixtures","دریافت و ذخیره مسابقات"},
            {"Auto fallback (recommended)","انتخاب خودکار سرویس"},{"API-Football","API-SPORTS"},{"API-Football (API key required)","API-SPORTS (نیازمند کلید)"},
            {"OpenLigaDB (no key; German leagues)","OpenLigaDB (بدون کلید؛ لیگ‌های آلمان)"},
            {"No upcoming fixtures found in the local database. Use Import fixtures to fetch schedules. / مسابقه‌ای در پایگاه داده نیست؛ ابتدا دریافت مسابقات را اجرا کن.","مسابقه‌ای در پایگاه داده نیست؛ ابتدا مسابقات را دریافت کن."},
            {"Fixture list uses data already saved locally. Refresh after importing new schedules.","فهرست از داده‌های ذخیره‌شده استفاده می‌کند. پس از دریافت، نوسازی کن."},
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
            foreach (var pair in Persian)
            {
                string english = pair.Key.Split('/')[0].Trim();
                if (IsPersian && (string.Equals(value, pair.Key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, english, StringComparison.OrdinalIgnoreCase))) return pair.Value;
                if (!IsPersian && (string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, pair.Key, StringComparison.OrdinalIgnoreCase))) return english;
            }
            return value;
        }
        public static string Market(string market, string outcome)
        {
            if (!IsPersian) return market + " — " + outcome;
            var markets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"Match result","نتیجه مسابقه"},{"Double chance","شانس دوگانه"},{"Both teams to score","گل‌زنی هر دو تیم"},
                {"Total goals","مجموع گل‌ها"},{"Home team goals","گل‌های میزبان"},{"Away team goals","گل‌های میهمان"},{"Corners","مجموع کرنرها"},{"Home team corners","کرنرهای میزبان"},{"Away team corners","کرنرهای میهمان"},{"Yellow cards","مجموع کارت زرد"},{"Red cards","مجموع کارت قرمز"},{"Shots on target","شوت‌های درون چارچوب"},{"Total shots","مجموع شوت‌ها"},{"Referee yellow cards","کارت زردهای داور"},{"VAR review","بررسی صحنه توسط VAR"},{"Total over 8.5","مجموع بیشتر از ۸٫۵"},{"Total under 8.5","مجموع کمتر از ۸٫۵"},{"Total over 3.5","مجموع بیشتر از ۳٫۵"},{"Total under 3.5","مجموع کمتر از ۳٫۵"},
                {"Home win","برد میزبان"},{"Draw","مساوی"},{"Away win","برد میهمان"},{"1X","برد میزبان یا مساوی (1X)"},{"X2","مساوی یا برد میهمان (X2)"},{"12","برد میزبان یا میهمان (12)"},{"Yes","بله"},{"No","خیر"},
                {"Over 0.5","بیشتر از ۰٫۵"},{"Under 0.5","کمتر از ۰٫۵"},{"Over 1.5","بیشتر از ۱٫۵"},{"Under 1.5","کمتر از ۱٫۵"},
                {"Over 2.5","بیشتر از ۲٫۵"},{"Under 2.5","کمتر از ۲٫۵"},{"Over 3.5","بیشتر از ۳٫۵"},{"Under 3.5","کمتر از ۳٫۵"},{"Over 4.5","بیشتر از ۴٫۵"},{"Under 4.5","کمتر از ۴٫۵"},
                {"Exactly 0","دقیقاً ۰ گل"},{"Exactly 1","دقیقاً ۱ گل"},{"Exactly 2","دقیقاً ۲ گل"},{"Exactly 3","دقیقاً ۳ گل"},{"4 or more","۴ گل یا بیشتر"}
            };
            string m, o;
            return (markets.TryGetValue(market, out m) ? m : market) + " — " +
                (markets.TryGetValue(outcome, out o) ? o : outcome);
        }
        public static void Apply(Window window)
        {
            window.FlowDirection = IsPersian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            ApplyTo(window);
        }
        private static void ApplyTo(DependencyObject parent)
        {
            foreach (DependencyObject child in GetLogicalChildren(parent))
            {
                var tb = child as TextBlock; if (tb != null) tb.Text = Translate(tb.Text);
                var button = child as Button; if (button != null && button.Content is string) button.Content = Translate((string)button.Content);
                var label = child as Label; if (label != null && label.Content is string) label.Content = Translate((string)label.Content);
                var comboItem = child as ComboBoxItem;
                if (comboItem != null && comboItem.Content is string) comboItem.Content = Translate((string)comboItem.Content);
                var column = child as DataGrid;
                if (column != null)
                    foreach (var gridColumn in column.Columns)
                        if (gridColumn.Header is string) gridColumn.Header = Translate((string)gridColumn.Header);
                ApplyTo(child);
            }
        }

        private static IEnumerable<DependencyObject> GetLogicalChildren(DependencyObject parent)
        {
            try
            {
                var children = LogicalTreeHelper.GetChildren(parent);
                return children == null ? Enumerable.Empty<DependencyObject>() : children.OfType<DependencyObject>().ToList();
            }
            catch { return Enumerable.Empty<DependencyObject>(); }
        }

        public static string League(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !IsPersian) return name;
            string v = name.Trim();
            var leagues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { {"Premier League","لیگ برتر انگلیس"},{"Bundesliga","بوندسلیگا"},{"La Liga","لالیگا"},{"Serie A","سری آ"},{"Ligue 1","لیگ یک فرانسه"},{"Persian Gulf Pro League","لیگ برتر ایران"},{"Iran Pro League","لیگ برتر ایران"},{"UEFA Champions League","لیگ قهرمانان اروپا"},{"UEFA Europa League","لیگ اروپا"},{"World Cup","جام جهانی"},{"FA Cup","جام حذفی انگلیس"} };
            string translated; return leagues.TryGetValue(v, out translated) ? translated : v;
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
    public static class ThemeService
    {
        private static readonly string ThemePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa", "theme.txt");
        public static string Mode { get; private set; } = "LightSage";

        public static void Load()
        {
            try
            {
                if (File.Exists(ThemePath))
                {
                    string value = File.ReadAllText(ThemePath).Trim();
                    if (new[] { "LightSage", "LightPearl", "LightOcean", "LightRose", "DarkMidnight", "DarkOcean", "DarkAmethyst", "DarkEmerald" }.Contains(value)) Mode = value;
                }
            }
            catch { Mode = "LightSage"; }
        }

        public static void SetMode(string mode)
        {
            Mode = new[] { "LightSage", "LightPearl", "LightOcean", "LightRose", "DarkMidnight", "DarkOcean", "DarkAmethyst", "DarkEmerald" }.Contains(mode) ? mode : "LightSage";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
                File.WriteAllText(ThemePath, Mode);
            }
            catch { }
        }

        private static bool IsDark()
        {
            if (Mode.StartsWith("Dark", StringComparison.Ordinal)) return true;
            if (Mode.StartsWith("Light", StringComparison.Ordinal)) return false;
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value != null) return Convert.ToInt32(value) == 0;
                }
            }
            catch { }
            return false;
        }

        public static void Apply(Window window)
        {
            if (window == null) return;
            bool dark = IsDark();
            Color background = dark ? Color.FromRgb(18, 22, 36) : Color.FromRgb(250, 249, 247);
            Color accentStart, accentEnd;
            switch (Mode)
            {
                case "LightSage": accentStart = Color.FromRgb(250, 252, 248); accentEnd = Color.FromRgb(215, 236, 222); break;
                case "LightPearl": accentStart = Color.FromRgb(255, 250, 242); accentEnd = Color.FromRgb(239, 224, 207); break;
                case "LightOcean": accentStart = Color.FromRgb(244, 251, 255); accentEnd = Color.FromRgb(204, 229, 242); break;
                case "LightRose": accentStart = Color.FromRgb(255, 247, 249); accentEnd = Color.FromRgb(244, 216, 226); break;
                case "DarkMidnight": accentStart = Color.FromRgb(17, 24, 50); accentEnd = Color.FromRgb(57, 38, 91); break;
                case "DarkOcean": accentStart = Color.FromRgb(7, 45, 73); accentEnd = Color.FromRgb(10, 86, 108); break;
                case "DarkAmethyst": accentStart = Color.FromRgb(39, 25, 66); accentEnd = Color.FromRgb(91, 47, 115); break;
                case "DarkEmerald": accentStart = Color.FromRgb(12, 48, 43); accentEnd = Color.FromRgb(17, 93, 76); break;
                default: accentStart = Color.FromRgb(245, 248, 246); accentEnd = Color.FromRgb(224, 234, 228); break;
            }
            window.Background = new LinearGradientBrush(accentStart, accentEnd, 35);
            ApplyTo(window, dark);
            var fixturesGrid = window.FindName("FixturesGrid") as DataGrid;
            if (fixturesGrid != null)
            {
                fixturesGrid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
                {
                    Setters = {
                        new Setter(Control.BackgroundProperty, new SolidColorBrush(dark ? Color.FromRgb(42, 48, 68) : Color.FromRgb(234, 241, 236))),
                        new Setter(Control.ForegroundProperty, new SolidColorBrush(dark ? Color.FromRgb(245, 247, 252) : Color.FromRgb(35, 45, 58))),
                        new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                        new Setter(Control.PaddingProperty, new Thickness(8, 10, 8, 10))
                    }
                };
            }
            var chart = window.FindName("MarketChartFrame") as Border;
            var reason = window.FindName("MarketChartReasonText") as TextBlock;
            if (chart != null && (reason == null || reason.Visibility != Visibility.Visible))
                chart.Background = new SolidColorBrush(dark ? Color.FromRgb(34, 42, 39) : Color.FromRgb(255, 255, 255));
        }

        private static void ApplyTo(DependencyObject parent, bool dark)
        {
            foreach (DependencyObject child in GetChildren(parent))
            {
                var element = child as FrameworkElement;
                if (element != null && element.Name != "MarketChartFrame" && element.Name != "MarketRowCard")
                {
                    var border = element as Border;
                    if (border != null && IsLightSurface(border.Background))
                        border.Background = border.CornerRadius.TopLeft >= 20 ? Brushes.Transparent :
                            new SolidColorBrush(dark ? Color.FromRgb(30, 35, 52) : Color.FromRgb(255, 255, 255));

                    var text = element as TextBlock;
                    if (text != null && !string.Equals(Convert.ToString(text.Tag), "ThemePreview", StringComparison.Ordinal))
                    {
                        text.Foreground = new SolidColorBrush(dark ? Color.FromRgb(242, 245, 252) : Color.FromRgb(35, 45, 58));
                    }

                    var grid = element as DataGrid;
                    if (grid != null)
                    {
                        grid.Background = new SolidColorBrush(dark ? Color.FromRgb(30, 35, 52) : Colors.White);
                        grid.Foreground = new SolidColorBrush(dark ? Color.FromRgb(242, 245, 252) : Color.FromRgb(35, 45, 58));
                        grid.AlternatingRowBackground = new SolidColorBrush(dark ? Color.FromRgb(37, 43, 61) : Color.FromRgb(246, 249, 247));
                        grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
                        grid.HorizontalGridLinesBrush = new SolidColorBrush(dark ? Color.FromRgb(64, 76, 70) : Color.FromRgb(228, 235, 230));
                    }

                    var combo = element as ComboBox;
                    if (combo != null)
                    {
                        combo.Background = new SolidColorBrush(dark ? Color.FromRgb(43, 52, 48) : Colors.White);
                        combo.Foreground = new SolidColorBrush(dark ? Color.FromRgb(238, 242, 239) : Color.FromRgb(36, 52, 44));
                    }
                    var input = element as TextBox;
                    if (input != null)
                    {
                        input.Background = new SolidColorBrush(dark ? Color.FromRgb(43, 52, 48) : Colors.White);
                        input.Foreground = new SolidColorBrush(dark ? Color.FromRgb(238, 242, 239) : Color.FromRgb(36, 52, 44));
                        input.CaretBrush = input.Foreground;
                    }
                    var list = element as ListBox;
                    if (list != null)
                    {
                        list.Background = new SolidColorBrush(dark ? Color.FromRgb(34, 42, 39) : Colors.White);
                        list.Foreground = new SolidColorBrush(dark ? Color.FromRgb(226, 233, 229) : Color.FromRgb(36, 52, 44));
                    }
                }
                ApplyTo(child, dark);
            }
        }

        private static bool IsLightSurface(Brush brush)
        {
            var solid = brush as SolidColorBrush;
            if (solid == null) return false;
            return solid.Color == Colors.White || solid.Color == Color.FromRgb(244, 247, 245) ||
                   solid.Color == Color.FromRgb(255, 255, 255) ||
                   ((int)solid.Color.R + solid.Color.G + solid.Color.B) / 3 >= 220;
        }

        private static bool IsDefaultTextColor(Brush brush)
        {
            var solid = brush as SolidColorBrush;
            if (solid == null) return false;
            return solid.Color == Color.FromRgb(22, 59, 43) || solid.Color == Color.FromRgb(94, 110, 101) ||
                   solid.Color == Color.FromRgb(64, 84, 73) || solid.Color == Color.FromRgb(122, 137, 128) ||
                   solid.Color == Color.FromRgb(36, 52, 44);
        }

        private static IEnumerable<DependencyObject> GetChildren(DependencyObject parent)
        {
            try
            {
                var children = LogicalTreeHelper.GetChildren(parent);
                return children == null ? Enumerable.Empty<DependencyObject>() : children.OfType<DependencyObject>().ToList();
            }
            catch { return Enumerable.Empty<DependencyObject>(); }
        }
    }
}
