# KoaZa Football Analytics

**A bilingual (Persian/English) Windows desktop application for football fixture collection and statistical analysis.**

> **Project status: In development.** Fixture import and provider configuration are being implemented. Historical-data-driven predictions, validation/backtesting, and a polished release workflow are not yet complete. Predictions should not be treated as guaranteed outcomes.

[فارسی](#راهنمای-فارسی) · [Features](#features) · [Requirements](#requirements) · [Build-and-run](#build-and-run) · [Data-providers](#data-providers) · [Security](#security) · [Roadmap](#roadmap)

## Features

- Windows desktop UI built with **C# / WPF**.
- Targets **.NET Framework 4.8**.
- Local **SQLite** database for storing imported fixtures.
- A persistent **Persian / English language setting** for the interface, fixture labels, market names, dates, and team names. Known club and national-team names use curated Persian names; unknown Latin-script names are transliterated as a fallback.
- Provider settings include **OddsPapi**, **Floodlight**, **Sports Open Data**, **OpenLigaDB**, **API-SPORTS**, and **SportsAPI360**, plus a manual provider option.
- Built-in fixture adapters: **API-SPORTS** (API key; league IDs and plan coverage apply), **OpenLigaDB** (no key; supported competitions), **OddsPapi** (API key; enter its own tournament ID), and **SportsAPI360** (API key; schedule endpoint covers the next seven days).
- **Floodlight**, **Sports Open Data**, and manually added providers use an HTTPS fixtures endpoint that returns a normalized JSON array/object (schema below). Their provider-specific authentication and JSON schemas may need adaptation to the vendor's documentation.
- The **Auto** provider mode tries compatible configured providers; it does not merge results across providers.
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

Change the global language from the selector at the top of the main window; the preference is saved for the current Windows user. Open **API Settings / تنظیمات API** to add, enable, disable, or remove providers. For **Floodlight**, **Sports Open Data**, or a manual provider, enter an HTTPS fixtures endpoint. Then open **Import Fixtures / دریافت مسابقات** and select a provider and its provider-specific competition/tournament identifier.

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

Provider coverage, API limits, competition identifiers, and historical availability can change. Check the provider's own documentation and account dashboard. Auto mode returns the first successful non-empty result; it does **not** combine or deduplicate data from every provider.

## Local data and credentials

- Fixture records are stored in a local SQLite database managed by the application.
- Provider keys are stored under the current Windows user's local application data directory and protected with **Windows DPAPI**.
- DPAPI-protected credentials are tied to the Windows user/machine context and should not be copied between user accounts as a backup strategy.
- **Never commit API keys, tokens, or other secrets to the repository.** If a key has been exposed, revoke or rotate it with the provider.

## Roadmap

- [x] WPF application foundation and SQLite integration
- [x] Provider settings UI and local protected credential storage (implementation in progress)
- [x] Fixture import adapters and cross-provider fallback (implementation in progress)
- [ ] Verify clean CI build and publish a successful portable artifact
- [ ] Read completed matches from SQLite and calculate team form and home/away scoring rates
- [ ] Connect historical statistics to the Poisson prediction model
- [ ] Add backtesting, calibration metrics, and clear confidence/uncertainty reporting
- [ ] Improve bilingual localization, validation, and user-facing error messages

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
- حالت **Auto** منابع سازگار را امتحان می‌کند و داده‌ها را از چند منبع با هم ادغام نمی‌کند.
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
4. از **Import Fixtures / دریافت مسابقات** سرویس و شناسه رقابت مخصوص همان سرویس را انتخاب کنید.
5. مسابقات دریافت‌شده در پایگاه داده محلی SQLite ذخیره می‌شوند.

نمونه کدهای حالت Auto:

| لیگ | کد |
|---|---|
| لیگ برتر انگلیس | `EPL` |
| لالیگا اسپانیا | `LALIGA` |
| بوندس‌لیگا آلمان | `BUNDESLIGA` |
| سری آ ایتالیا | `SERIEA` |
| لیگ یک فرانسه | `LIGUE1` |
| لیگ برتر ایران | `IRAN` |

پوشش واقعی هر لیگ به منبع داده، مجوز حساب، فصل درخواستی و سهمیه API بستگی دارد. در حالت Auto، برنامه از اولین منبعی که اطلاعات غیرخالی برگرداند استفاده می‌کند؛ داده‌های چند منبع را با هم ادغام نمی‌کند.

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
