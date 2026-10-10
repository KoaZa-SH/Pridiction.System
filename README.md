# KoaZa Football Analytics

**A bilingual (Persian/English) Windows desktop application for football fixture collection and statistical analysis.**

> **Project status: in development.** Fixture import, multi-provider configuration, local history storage, a Poisson goal baseline, and historical-frequency estimates for selected statistical markets are implemented. Automated backtesting, probability calibration, a comprehensive automated test suite, and production-grade release validation remain outstanding. Predictions are estimates, not guarantees.

[فارسی](#راهنمای-فارسی) · [Features](#features) · [Requirements](#requirements) · [Build-and-run](#build-and-run) · [Data-providers](#data-providers) · [Security](#security) · [Roadmap](#roadmap)

## Features

- Windows desktop UI built with **C# / WPF**.
- Targets **.NET Framework 4.8**.
- Local **SQLite** database for storing imported fixtures.
- A persistent **Persian / English language setting** for the interface, fixture labels, market names, dates, and team names. Known club and national-team names use curated Persian names; unknown Latin-script names are transliterated as a fallback.
- Provider settings include **API-SPORTS**, **football-data.org**, **OpenLigaDB**, **SportsAPI360**, **5DollarFootballAPI**, **Football-Charts**, **OddsPapi**, **Floodlight**, **Sports Open Data**, and a manual provider option. Coverage and authentication vary by provider.
- Built-in fixture adapters include **API-SPORTS** (date-range fixture queries across competitions covered by the account), **OpenLigaDB** (no key for supported competitions), **OddsPapi** (provider-specific tournament ID), **SportsAPI360**, **5DollarFootballAPI**, and **Football-Charts**. Actual competition coverage, endpoint access and quota depend on the provider account and configuration.
- When a fixture is selected, the app can fetch and cache the provider's fixture details (statistics, events, lineups and player data when available) in SQLite. Live fixtures with scores are not filtered out just because goals are already recorded.
- A match-specific news panel searches Google News RSS for both teams and the competition; it requires an internet connection but no separate API key. News headlines are context, not verified causal impact or a sentiment-based prediction.
- **Floodlight**, **Sports Open Data**, and manually added providers use an HTTPS fixtures endpoint that returns a normalized JSON array/object (schema below). Their provider-specific authentication and JSON schemas may need adaptation to the vendor's documentation.
- The **Auto** fixture refresh queries API-SPORTS across each of the next seven UTC dates and live fixtures, then merges/deduplicates compatible enabled football-data.org, OpenLigaDB, SportsAPI360 and custom feeds. It is not restricted to the English Premier League: it can include leagues, domestic cups, continental club competitions, national-team tournaments and Olympics fixtures when the provider covers them.
- Credentials are stored locally using Windows DPAPI.
- GitHub Actions workflow builds the Windows application and can publish a portable build artifact when the build succeeds.

## Requirements

- Windows with .NET Framework 4.8 available.
- Internet connection for live provider requests.
- API credentials for providers that require them.
- No local Visual Studio installation is required to download a published build artifact; contributors can use Visual Studio/MSBuild to develop locally.

## Build and run

### Download a build

1. Open the repository's [Actions](../../actions) page.
2. Open the latest successful **Build KoaZa WPF app** workflow run.
3. In **Artifacts**, download `KoaZa-Windows-Build`.
4. Extract the archive and launch the application executable.

Only use a run with a **successful** status. If the workflow is failing or no artifact is published, a usable build is not available from that run.

### Build from source

Open `src/KoaZa/KoaZa.sln` in Visual Studio with .NET desktop development tools, or build with MSBuild on a compatible Windows environment. Restore NuGet packages before compiling.

The GitHub Actions workflow is defined in `.github/workflows/build-windows.yml`.

## Data providers

Change the global language from the selector at the top of the main window; the preference is saved for the current Windows user. Open **API Settings / تنظیمات API** to add, enable, disable, or remove providers. For **Floodlight**, **Sports Open Data**, or a manual provider, enter an HTTPS fixtures endpoint. Then open **Import Fixtures / دریافت مسابقات**. Choose **Auto fallback** to refresh the next seven days of fixtures across competitions covered by API-SPORTS and other compatible enabled feeds. Selecting an individual provider still uses that provider's own competition/tournament identifier.

### Provider and competition examples

| Competition | Auto code | Notes |
|---|---|---|
| English Premier League | `EPL` | Supported through providers configured with access to this competition |
| Spanish La Liga | `LALIGA` | Supported through configured providers |
| German Bundesliga | `BUNDESLIGA` | OpenLigaDB may be an option |
| Italian Serie A | `SERIEA` | Coverage depends on provider |
| French Ligue 1 | `LIGUE1` | Coverage depends on provider |
| Iranian Pro League | `IRAN` | Currently mapped to API-Football; availability depends on its coverage and your plan |

For manual endpoints, the response should be a JSON array or an object containing a `response`, `matches`, `fixtures`, or `data` array. Each row should include `homeTeam` and `awayTeam` (strings or objects with a `name`), plus a date field such as `date`, `kickoff`, or `utcDate`; optional fields are `league`, `homeGoals`, `awayGoals`, `status`, and `id`. The app sends the configured key in the authentication header (default `x-api-key`); the header name can be changed in API Settings.

Provider coverage, API limits, competition identifiers, and historical availability can change. Check the provider's own documentation and account dashboard. Global refresh does not guarantee every league or every cup worldwide; it can only import fixtures visible to the configured provider, subscription and API quota. API-SPORTS statistics may include shots, fouls, corners, possession, yellow/red cards, events and lineups, but availability varies by competition and match.

## Local data and credentials

- Fixture records are stored in a local SQLite database managed by the application.
- Provider keys are stored under the current Windows user's local application data directory and protected with **Windows DPAPI**.
- DPAPI-protected credentials are tied to the Windows user/machine context and should not be copied between user accounts as a backup strategy.
- **Never commit API keys, tokens, or other secrets to the repository.** If a key has been exposed, revoke or rotate it with the provider.

## Roadmap

- [x] WPF application foundation and SQLite integration
- [x] Provider settings UI and local protected credential storage (implementation in progress)
- [x] Bulk fixture refresh for the next seven days and live matches, with compatible feed merging/deduplication
- [x] Match statistics/events cache and team-specific Google News RSS panel
- [x] Verify a clean CI build and publish a portable artifact from a successful workflow run
- [x] Read completed results from SQLite and estimate team scoring/conceding rates with home/away context
- [x] Refine the Poisson goal baseline with historical head-to-head, shots-on-target and a small corners signal when available; estimate selected corners/cards/shots/VAR markets from historical frequencies
- [ ] Add chronological walk-forward backtesting, calibration metrics (Brier score/log loss), and clear confidence/uncertainty reporting
- [ ] Improve bilingual localization, validation, and user-facing error messages

## Match news and detailed statistics

The news panel uses the Google News RSS search endpoint, so there is no required news API key. It searches for the selected home/away teams and competition, and opens the original publisher's article. RSS availability and relevance depend on the network and news index. It does not claim that a headline caused a match outcome or automatically assign a verified impact score. If you later need structured news sentiment, injury/news classification or a commercial SLA, add a dedicated provider such as GNews or another licensed news API and review its current pricing, coverage and terms.

Detailed match information is requested from API-SPORTS when a match is selected and cached locally. The API's `fixtures?id=...` response can contain match statistics, events, lineups and player data when covered; the separate injuries endpoint is also requested for non-live matches when available. A missing statistic means unavailable, not zero. The amount of data depends on the competition and subscription.

## Limitations and responsible use

Football matches are uncertain. Statistical outputs are estimates, not guarantees or financial advice. Do not assume that an API returning no matches means the competition has no fixtures; access restrictions, competition IDs, seasons, quotas, or provider outages may be responsible.

## Contributing

Bug reports and focused pull requests are welcome. Please include the operating system, provider name (never the API key), competition code, season, and the relevant error message when reporting an import problem.

---

# راهنمای فارسی

**KoaZa یک نرم‌افزار ویندوزی برای دریافت اطلاعات مسابقات فوتبال و تحلیل آماری آن‌هاست.**

> **وضعیت پروژه: در حال توسعه.** بخش دریافت و ذخیره‌سازی مسابقات در حال پیاده‌سازی است. پیش‌بینی مبتنی بر تاریخچه، آزمون مدل و انتشار نسخه پایدار هنوز کامل نشده‌اند؛ بنابراین خروجی‌ها را نتیجه قطعی مسابقات تلقی نکنید.

## امکانات فعلی و در حال توسعه

- رابط دسکتاپ با **C# و WPF**.
- هدف‌گذاری برای **.NET Framework 4.8**.
- ذخیره اطلاعات مسابقات در پایگاه داده محلی **SQLite**.
- تنظیم زبان سراسری **فارسی / انگلیسی** که ذخیره می‌شود و روی رابط، تاریخ‌ها، نام بازارها و نام تیم‌ها اثر می‌گذارد. نام تیم‌های شناخته‌شده ترجمه شده و برای نام‌های ناشناخته، آوانویسی فارسی به‌عنوان جایگزین استفاده می‌شود.
- تنظیمات منابع شامل **OddsPapi**، **Floodlight**، **Sports Open Data**، **OpenLigaDB**، **API-SPORTS** و **SportsAPI360** است؛ افزودن منبع دستی نیز وجود دارد.
- اتصال آماده برای دریافت مسابقات: **API-SPORTS**، **OpenLigaDB**، **OddsPapi** و **SportsAPI360**. شناسه لیگ/رقابت و محدودیت طرح اشتراک هر سرویس را جداگانه بررسی کنید.
- برای **Floodlight**، **Sports Open Data** و منابع دستی، نشانی HTTPS داده مسابقات لازم است و پاسخ باید با قالب JSON مشخص‌شده در بخش انگلیسی سازگار باشد.
- حالت **Auto** مسابقات هفت روز آینده را برای تمام رقابت‌هایی که API-SPORTS پوشش می‌دهد دریافت می‌کند، مسابقات زنده را نیز می‌گیرد و در صورت فعال بودن، منابع سازگار SportsAPI360 و منبع دستی را ادغام و تکراری‌ها را حذف می‌کند. این روش فقط به لیگ برتر انگلیس محدود نیست؛ جام‌های داخلی، رقابت‌های باشگاهی قاره‌ای، تورنمنت‌های ملی و مسابقات المپیک نیز در صورت پوشش منبع قابل دریافت‌اند.
- کلیدها با Windows DPAPI به‌صورت محلی محافظت می‌شوند.
- ساخت برنامه ویندوز از طریق GitHub Actions؛ فایل قابل‌دریافت فقط پس از موفقیت ساخت منتشر می‌شود.

## دریافت نسخه آماده

1. وارد بخش [GitHub Actions](../../actions) شوید.
2. آخرین اجرای موفق با نام **Build KoaZa WPF app** را باز کنید.
3. از قسمت **Artifacts** فایل `KoaZa-Windows-Build` را دریافت کنید.
4. فایل فشرده را استخراج و فایل اجرایی برنامه را اجرا کنید.

**فقط از اجرای موفق استفاده کنید.** اگر ساخت ناموفق باشد یا Artifact منتشر نشده باشد، آن اجرا نسخه آماده‌ای برای دریافت ندارد.

## روش دریافت مسابقات

1. برنامه را اجرا کنید و از انتخابگر بالای صفحه زبان **فارسی** یا **English** را انتخاب کنید.
2. وارد **API Settings / تنظیمات API** شوید و سرویس و کلیدهای لازم را اضافه کنید.
3. برای Floodlight، Sports Open Data یا سرویس دستی، نشانی HTTPS endpoint را وارد کنید.
4. از **Import Fixtures / دریافت مسابقات** گزینه **Auto fallback** را انتخاب کنید تا تقویم هفت روز آینده و مسابقات زنده از منابع سازگار دریافت شود. انتخاب مستقیم یک سرویس همچنان از شناسه مخصوص همان سرویس استفاده می‌کند.
5. مسابقات دریافت‌شده در پایگاه داده محلی SQLite ذخیره می‌شوند. هنگام انتخاب مسابقه، آمار و رویدادهای در دسترس از API-SPORTS دریافت و ذخیره می‌شود.

نمونه کدهای حالت Auto:

| لیگ | کد |
|---|---|
| لیگ برتر انگلیس | `EPL` |
| لالیگا اسپانیا | `LALIGA` |
| بوندس‌لیگا آلمان | `BUNDESLIGA` |
| سری آ ایتالیا | `SERIEA` |
| لیگ یک فرانسه | `LIGUE1` |
| لیگ برتر ایران | `IRAN` |

پوشش واقعی هر لیگ و جام به منبع داده، مجوز حساب و سهمیه API بستگی دارد. دریافت سراسری به معنی تضمین همه مسابقات جهان نیست؛ فقط رقابت‌هایی دریافت می‌شوند که منبع فعال پوشش می‌دهد. آمار تفصیلی ممکن است شامل شوت، خطا، کرنر، مالکیت توپ، کارت‌ها، رویدادها، ترکیب و اطلاعات بازیکنان باشد؛ داده ناموجود به‌معنی صفر نیست.

## اخبار و آمار تفصیلی مسابقه

بخش اخبار از Google News RSS استفاده می‌کند و به کلید API جداگانه نیاز ندارد؛ اتصال اینترنت لازم است. خبرها بر اساس نام هر دو تیم و رقابت جست‌وجو می‌شوند و با کلیک روی تیتر، منبع اصلی باز می‌شود. این بخش تضمین نمی‌کند خبر واقعاً علت تغییر نتیجه است یا میزان اثرگذاری آن را به‌طور قطعی محاسبه نمی‌کند. برای تحلیل ساختاریافته احساسات خبر، دسته‌بندی مصدومیت یا سرویس تجاری پایدار، می‌توان بعداً یک ارائه‌دهنده تخصصی خبر مانند GNews یا سرویس مجاز دیگری اضافه کرد؛ هزینه، پوشش و شرایط استفاده آن باید جداگانه بررسی شود.

هنگام انتخاب مسابقه، جزئیات قابل‌دسترس از API-SPORTS دریافت و در SQLite ذخیره می‌شود. داده‌های آمار، رویدادها، ترکیب‌ها، بازیکنان و در صورت دسترسی اطلاعات مصدومیت به پوشش همان رقابت و طرح اشتراک وابسته‌اند.

## امنیت کلیدهای API

- کلیدها و توکن‌ها را در کد یا مخزن GitHub قرار ندهید.
- اطلاعات ورود در پوشه محلی کاربر ویندوز نگهداری و با DPAPI محافظت می‌شود.
- اگر کلیدی افشا شد، آن را از پنل ارائه‌دهنده باطل و جایگزین کنید.
- کلید محافظت‌شده با DPAPI برای انتقال ساده به حساب ویندوز یا دستگاه دیگر طراحی نشده است.

## نقشه راه

- [x] پایه برنامه WPF و اتصال SQLite
- [x] رابط تنظیمات منابع و ذخیره محلی کلیدها (در حال تکمیل و بررسی)
- [x] اتصال منابع دریافت مسابقات و تلاش جایگزین بین منابع (در حال تکمیل و بررسی)
- [ ] تأیید ساخت موفق در GitHub Actions و انتشار نسخه قابل‌حمل
- [ ] محاسبه فرم تیم‌ها و نرخ گل‌زنی/گل‌خوردن از مسابقات گذشته
- [ ] اتصال آمار تاریخی به مدل پیش‌بینی پواسون
- [ ] افزودن ارزیابی گذشته‌نگر، سنجش دقت و نمایش عدم‌قطعیت
- [x] افزودن انتخاب زبان فارسی/انگلیسی و ترجمه نام تیم‌ها
- [x] افزودن سرویس‌های API درخواستی و امکان ثبت منبع دستی

## هشدار

فوتبال ذاتاً غیرقطعی است. پیش‌بینی آماری تضمین نتیجه نیست و نباید به‌عنوان توصیه مالی تلقی شود. خالی‌بودن پاسخ API لزوماً به معنی نبود مسابقه نیست؛ محدودیت دسترسی، شناسه لیگ، فصل، سهمیه درخواست یا قطعی سرویس هم می‌تواند علت باشد.

## مشارکت در پروژه

برای گزارش خطا، سیستم‌عامل، نام ارائه‌دهنده (بدون کلید API)، کد لیگ، فصل و متن خطا را ارسال کنید. هرگز کلید یا توکن محرمانه را در گزارش عمومی قرار ندهید.
