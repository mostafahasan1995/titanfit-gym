# TitanFit Gym System: Plan

The single source of truth for this project. Any session that continues the work starts here.

## Status

| Step | What | Status |
|---|---|---|
| 1 | Design mockups | ✅ Done (`design/`, logo and name added) |
| 2 | Core: skeleton, CI, database, API, login, subscription logic, installers | ✅ Done (2.1–2.5) |
| B0 | App foundation: API abstraction + sample data, station.json, login and roles, page shell, shared controls, smoke screenshots per page | ✅ Done (see "App foundation") |
| 3 | Reception app | ✅ Done (see "Reception app"); the ID barcode parser is a placeholder until a real scan arrives (open item 1) |
| 4 | Cafeteria app | ✅ Done (see "Cafeteria app"); receipt printer and scanner still to test on site |
| 5 | Admin app + deployment | ✅ Done: Admin app, daily backups, final installers, release **v0.1.0** (see "Admin app", "Backups", "Installers"). Only the on-site hardware test is left |
| 6 | Money in US dollars + daily exchange rate (owner, 2026-11-28) | ✅ Done (see "Money and the exchange rate"): all amounts in USD, the manager's daily rate with history, payments in USD or SYP, SYP shown next to every price; migration `UsdAndExchangeRates`. Release v0.2.0 is cut by the coordinator |
| R1 | Responsive UI (owner, 2026-11-28: UI too small, buttons cut off at the bottom) | ✅ Done: windows open maximized, per-monitor DPI, UI drawn larger on big screens, every page fits 1366×768 at 125 %, Save / Pay / Check-in always visible; CI checks every page at 4 screen sizes (see "Screen sizes") |

**Next action:** the on-site test at the gym (follow "First install at the gym"; hardware in open items 2, 4, 13 and 14), and the owner's inputs: an ID card scan sample (open item 1), the receipt printer model (2), the fingerprint reader model (10), the backup drive (8), the theme choice Deep Teal or Light (16), plan names and member deactivation (15). The owner said on 2026-11-26 to execute all steps without stopping; report at milestones.

## Requirements (from the gym)

- **Admin:** one PC. It stays on all day, but the admin account in the program stays logged out.
- **Reception (2 PCs):**
  - Take member photos with a camera.
  - Scan the Syrian ID card barcode to fill in the ID data.
  - Member info = ID data + phone number.
  - Subscription length in months, and a check for whether it has ended.
  - Check-in: the member shows a QR on their phone; the camera reads it.
- **Cafeteria (2 PCs):** stock management and selling items (cashier).
- **Language:** Arabic and English, switchable (Arabic is right-to-left).
- **Touchscreens:** maybe later (not bought yet). Design for touch and mouse.
- **UI:** a polished theme tied to the gym's identity (TITANFIT).

## Architecture

```
Admin PC (server, on all day)
 ├─ PostgreSQL
 ├─ Gym.Api  ← ASP.NET Core + SignalR, runs as a Windows service
 │             (keeps working while nobody is logged in to the admin app)
 └─ Admin app
        ▲  LAN only, HTTP + SignalR on port 5080 (HTTPS later, open item 12), no internet needed
        │
 ├─ Reception PC 1, Reception PC 2  → Reception app (webcam + ID scanner)
 └─ Cafeteria PC 1, Cafeteria PC 2  → Cafeteria app (barcode scanner + receipt printer)
```

- All apps talk only to the API, never directly to the database:
  - The business rules (subscription status, stock) live in one place.
  - The database password is not stored on every PC.
  - Changes show up live on every PC (SignalR), e.g. both cashiers see the same stock.

## Stack

| Part | Technology |
|---|---|
| Language | C# on .NET 10 (LTS) |
| Database | PostgreSQL + EF Core (Npgsql) |
| Server | ASP.NET Core Web API + SignalR, hosted as a Windows service |
| Desktop apps | WPF + CommunityToolkit.Mvvm + WPF-UI, plus our own theme library |
| Camera | OpenCvSharp4 |
| QR | ZXing.Net (read), QRCoder (create) |
| ID card | USB 2D barcode scanner in keyboard (HID) mode; reads PDF417, QR, 1D |
| Charts | LiveCharts2 (admin dashboard) |
| Tests | xUnit |
| Build | GitHub Actions (Windows runner) + Inno Setup installers |

## Repository layout

```
src/
  Gym.Domain          entities + rules (subscription status, stock), pure C#
  Gym.Contracts       request/response types, roles, SignalR events (shared by API and client)
  Gym.Infrastructure  EF Core, PostgreSQL, migrations, data services
  Gym.Api             REST + SignalR + role-based auth
  Gym.Client          shared typed API client + SignalR connection for the apps
  Gym.UI              shared app foundation: theme, styles, controls, fonts, logo, station settings,
                      login, page shell, localization, smoke screenshots
  Gym.Admin           WPF app (admin PC)
  Gym.Reception       WPF app (2 reception PCs)
  Gym.Cafeteria       WPF app (2 cafeteria PCs)
tests/
  Gym.Tests              xUnit: domain rules, API without a database (Windows job)
  Gym.IntegrationTests   xUnit: PostgreSQL + API tests (Linux job, postgres service)
installer/            Inno Setup scripts (server.iss, station.iss), setup PowerShell scripts, CI build + install smoke test
design/               UI mockups (step 1)
docs/PLAN.md          this file
.github/workflows/    CI build; ef-migration (generates EF migrations on a runner)
```

## Business rules

- **Subscription:**
  - `EndDate = StartDate.AddMonths(months)`, by calendar months. A missing day clamps to the month's last day (31 Jan + 1 month = 28/29 Feb).
  - One domain function, `GetStatus(today)`, returns:
    - `Expired` when `today > EndDate` (overdue days = `today - EndDate`).
    - `ExpiringSoon` when 0 to 3 days are left (`EndDate - today`). The threshold of 3 is a setting.
    - `Active` otherwise.
  - The end date itself is still valid ("expires today").
  - Check-in, search and the admin lists all use `GetStatus`.
  - Unit tests must cover month-end and leap-year cases.
  - **Renewal (decided, open item 5):** while the current period is still valid (up to and including its end date), the new period starts the day after the old end date, so no paid day is lost or counted twice. After expiry (or for a first subscription) it starts today. The same rule chains private training services. Two stations renewing the same member at once still chain (the member row is locked).
    - Example: 1 month bought on 26 Nov → valid up to and including 26 Dec. Renewed on 20 Dec for 1 month → 27 Dec to 27 Jan. Renewed only on 5 Jan (expired) → 5 Jan to 5 Feb.
  - A member's status is the status of his latest membership end date. A member who never paid has no status (check-in result "no subscription").
- **Plans:** 1 / 3 / 6 / 12 months, prices editable by the admin. Sample prices: $20 / $55 / $100 / $180. The price paid is copied into the subscription; later price changes do not touch it.
- **Private trainer:** a member of type `PrivateTrainer`. He pays the normal membership, and for training clients the "private training service": price = months × one fixed monthly fee from the settings (default $25, sample; admin edits it). The fee is read at the time of payment and stored with it (decided). Only a private trainer can be linked as a client's trainer; each client keeps his own membership. A private trainer with linked clients cannot be changed to regular until they are unlinked.
- **Staff:** type Admin, Reception, Cashier or Club trainer. The login is optional per staff member (username + password + role Admin/Reception/Cashier); club trainers usually have none. Club trainers get one rating per month (1 to 5 stars + note); setting a month again replaces it; all months are kept.
- **Money (decided 2026-11-28, see "Money and the exchange rate"):** every amount is stored in US dollars with cents (plans, the private training fee, cafeteria items, sales, payments, shifts, reports). The manager enters the day's rate (Syrian pounds for $1) in the Admin app; screens and receipts show the USD amount and the SYP equivalent. A customer pays in USD or SYP, cash.
- **Member QR:**
  - Holds a random member code, never the national ID.
  - Reissuing a QR makes the old one invalid.
  - Shown on the reception screen with Copy image / Save image, so staff can send it by WhatsApp. No WhatsApp API, no internet.
- **Member number:** short sequential display number (e.g. #00412), separate from the database ID.
- **Check-in:** every scan is logged (member, time, station, result).
- **Stock:**
  - Every change is a stock movement row (sale, stock-in, adjustment). Current stock = sum of movements.
  - A sale saves its lines and stock movements in one transaction, so 2 tills can sell at the same time.
  - Low stock = stock below the item's minimum.
  - **Decided (step 2.2, the owner can change it):** stock never goes below zero. A sale line above the current stock is refused with the error `insufficient_stock` (listing each short item), and so is an adjustment that would leave a negative stock.
  - The product rows are locked during a sale, so 2 tills selling the last items at the same moment cannot oversell (tested).
  - A sale belongs to the cashier's open shift; selling without an open shift opens one.
- **Roles:** Admin, Reception, Cashier. Staff log in with a username and password. Admin can do everything; see "API overview".

## Features

**Reception**
- **New member:**
  1. Scan the ID, which fills in the fields.
  2. Add the phone number (+963).
  3. Take a photo with the webcam.
  4. Choose the number of months: the end date is calculated and the payment is saved.
  5. Save: the member's QR is created.
- **ID fields (guessed, must be checked against a real scan):** First name, Father's name, Surname, Mother's name, Birth date, Birth place, National number (الاسم، اسم الأب، النسبة، اسم الأم، تاريخ الولادة، مكان الولادة، الرقم الوطني).
- **Renew and search:** find a member by name, phone, national number or QR.
- **Check-in:** the camera reads the QR. A big result card shows the photo, name and status, with a sound:
  - Green: Active, N days left.
  - Amber: expires in N days.
  - Red: expired N days ago, with a Renew button.
- Manual entry of the member number as a fallback.

**Cafeteria**
- **Cashier:**
  - Product grid made for touch, with categories (Drinks, Shakes, Snacks, Supplements).
  - Barcode search, cart with +/-, Charge, then a thermal receipt.
- **Stock:** items, stock-in, adjustments, low-stock warnings.
- **Shift close:** sales summary per cashier and shift.

**Admin**
- **Dashboard:** today's check-ins (by hour), active / expiring / expired members, revenue, low stock, members expiring soon.
- **Management:** members, plans and prices, cafeteria items, staff users and roles, settings.
- **Reports:** subscriptions, attendance, sales.
- **Backups:** daily automatic database backup (to an external drive).

## Money and the exchange rate (step 6)

Owner decisions of 2026-11-28 (all confirmed). The system was not installed yet, so the old amounts were simply reinterpreted as dollars.

- **Storage:** all money in US dollars, `numeric(14,2)` (`decimal` in C#): plan prices, the private training fee, product prices, sale lines and totals, subscriptions (`price_paid`), shift cash, reports. Sample prices are realistic USD values (plans $20 / $55 / $100 / $180, fee $25, water $0.25 … whey $38).
- **Daily rate:** the manager types today's rate (Syrian pounds for $1, above 0) on the Admin app's **Exchange rate** page. Every entry is kept (`exchange_rates`: date = the server's local day, rate, entered by, entered at); a second entry on the same day becomes the one in use. The rate in use is always the newest entry (`CurrencyRules.GetState`): entered today → normal; from an earlier day → still used, with an amber warning banner in all three apps ("today's rate has not been entered yet, the last rate is used: $1 = 11,500 SYP (entered on …)"); none at all → amounts in SYP are hidden, a banner says to enter a rate, and only USD can be taken. Warnings only: nothing is blocked.
- **Conversion and rounding:** SYP = USD × rate, rounded to the nearest multiple of the **rounding step** (Admin > Settings, whole pounds, default 1; a half goes up) (`CurrencyRules.ToSyp`). Each payment is converted on its own.
- **Payments:** every payment place (new member, renew, private training service, cafeteria sale) has a "Pay in: $ / SYP" picker. Stored per payment (subscriptions and sales): `currency` (Usd / Syp), `paid_amount` (the amount in that currency: the dollars, or the rounded pounds), `exchange_rate` (the rate used; also kept with a dollar payment when a rate exists) and the dollar value (`price_paid` / `total`). A sale also keeps `cash_received` and gives the change in the paid currency (`cash_received_too_low` against the amount due in that currency). The station sends the rate it showed (`PaymentRequest.ExchangeRateId`), so the pounds the customer saw are the pounds stored; without it the server uses the newest rate. SYP without any rate: `no_exchange_rate`.
- **Reports:** totals in USD; subscriptions, sales and the shift summary also give `Paid` (`CurrencyTotals`: dollars taken, pounds taken and their dollar value, counts). Shift expected cash = opening + sales, in dollars.
- **Display:** `Money.Format` = USD ("$12.50", both languages; in Arabic wrapped in marks so it keeps its place in a sentence), `Money.FormatSyp` = whole pounds ("230,000 ل.س"), `Money.FormatWithSyp`, `Money.Parse` (USD, up to 2 decimals) / `Money.ParseSyp`. Controls: `MoneyText` (USD, or `Currency="Syp"`), `SypText` (the SYP equivalent of a USD amount at the day's rate, hidden without a rate), `CurrencyPicker`. `Gym.UI.ExchangeRates.Current` is the station's rate: loaded at login, updated by the live event `ExchangeRateChanged` and reloaded every 5 minutes; the shell shows a rate chip in the header and the warning banner under it.
- **API:** `GET /api/exchange-rates/current` (A R C: `ExchangeRateStatusDto` today, newest rate, state, rounding step), `POST /api/exchange-rates` (A: `{ sypPerUsd }`), `GET /api/exchange-rates?from=&to=` (A: history, newest first). Payment bodies take `payment: { currency, exchangeRateId }`.
- **Migration:** `UsdAndExchangeRates` (bigint → numeric(14,2), the new payment columns, `settings.syp_rounding_step`, `exchange_rates`; it copies the old amounts into `paid_amount` and moves the untouched default plan prices to the USD sample prices).

## UI and theme (TITANFIT)

- **Design files:** `design/*.dc.html`.
  - Live canvas, private to the owner: https://claude.ai/artifact/XqK7rQgsHL4pqLxNmuB6LD
  - The files need the Design canvas runtime to render. Read them as the reference for layout, EN/AR text and colors.
- **Screens designed:**
  - Reception check-in (`Main`)
  - Reception new member (`Reception-Register`)
  - Cafeteria cashier (`Cafeteria-POS`)
  - Admin dashboard (`Admin-Dashboard`)
- **Name:** TITAN**FIT** ("FIT" in brand cyan).
- **Logo:** gold crown with cyan gems over a white "T" and gold steps.
  - Source: the inline SVG in the sidebar of each design file.
  - Convert it to XAML geometry in `Gym.UI`.
- **Colors ("Deep Teal", chosen by the owner in step 2.1; replaces the graphite of the mockups):**

| Token | Value |
|---|---|
| Background | `#0A1C22` |
| Sidebar | `#0C2830` (vertical gradient to `#0A1F26` at the bottom) |
| Panel | `#0F2731` |
| Panel 2 | `#133340` |
| Line | `#1B3E4A` |
| Border | `#24505D` |
| Text | `#EEF7F9` |
| Text secondary | `#9DBCC4` |
| Text tertiary | `#C4DCE1` |
| Brand / accent (aqua) | `#22D8F0` |
| Turquoise (secondary accent) | `#14C2B8` |
| Accent gradient | `#22D8F0` → `#14C2B8`, left to right: primary buttons, active nav item, thin top accent line |
| Accent soft fill | `#22D8F0` at 12 % opacity: selected rows, active nav background, info chips |
| Gold (money, renew, premium) | `#D6A93A` (gradient `#F2CF6B` → `#D6A93A` → `#B8892A`) |
| Text on accent | `#04181D` |
| Status good | `#3DD68C` |
| Status warning | `#F5B83D` |
| Status bad | `#F26D6D` |

- **Theme code:** all colours are resources in one dictionary, `Gym.UI/Themes/Colors.xaml`. Styles and controls use them through `DynamicResource`, so a later light theme is a swap of that one file. The logo keeps its fixed brand colours.

- **Status colors** always come with an icon and a label, never color alone.
- **Fonts:**
  - Alexandria (headings, numbers) and IBM Plex Sans Arabic (body). Both are Google Fonts (OFL).
  - Embedded in `Gym.UI/Fonts` with their `OFL.txt`, taken from the google/fonts GitHub repo.
  - google/fonts has Alexandria only as a variable font, which WPF cannot use for weights. The static Medium / SemiBold / Bold / ExtraBold files are instances of it made with fontTools (allowed by the OFL; Alexandria has no Reserved Font Name).
- **RTL:**
  - The whole UI flips with `FlowDirection` in Arabic.
  - Syrian month names (كانون الثاني … كانون الأول).
  - Western digits 0-9.
- **Touch:** buttons and targets 44 to 64 px. Check-in result readable from 2 m away.
- **Screen sizes** (step R1; the owner's PCs: 1366×768 and 1920×1080, at 100 % or 125 % Windows scaling):
  - Root cause of the v0.1.0 problem: the window opened at a fixed 1440×900 with a minimum of 1024×640, larger than a 1366×768 screen at 125 % (1093×566 logical px above the taskbar), so the bottom of every screen (Save, Charge, Close shift) was below the screen edge; pages and dialogs had fixed sizes and nothing scrolled; on a 1920×1080 screen at 100 % the window stayed 1440×900 at design size ("too small").
  - The window opens maximized on the work area (taskbar visible), minimum 900×520. The apps are per-monitor V2 DPI aware (`Gym.UI/app.manifest`, checked by `--self-test`), so Windows scaling (100 %, 125 %, 150 %) is sharp.
  - `UiScaleHost` (`Gym.UI/Responsive.cs`) draws the whole UI larger on big windows, never smaller: scale = min(width / 1366, height / 820), 1 to 1.6 (Reception, a touch screen read from a distance: reference 1280×768). 1920×1080 at 100 %: Admin and Cafeteria ×1.22, Reception ×1.3.
  - Breakpoints from the logical size the UI gets, inherited by every element: `ui:Responsive.IsShort` (< 720 px high: 1366×768 at 100 % and 125 %), `IsVeryShort` (< 600: 1366×768 at 125 %), `IsNarrow` (< 1200 wide: 1366×768 at 125 %). Styles use them in triggers; `ui:Responsive.CollapseWhenShort` / `CollapseWhenVeryShort` / `CollapseWhenNarrow` hide a secondary element (the big login logo, the recent check-ins, the shift card on the Sell screen).
  - Layout rules for every screen: the main action (Save, Charge, Check in, Renew, Close shift, a dialog's buttons) is docked outside the scrolling part and always visible; forms and side panels scroll (`ScrollViewer`, vertical only); side columns are proportional with a minimum and a maximum (`Width="0.75*" MinWidth="320" MaxWidth="400"`) instead of fixed; table columns are shares with a minimum; toolbars wrap (`controls:SplitRow`, `WrapPanel`); card rows and form columns wrap (`controls:ResponsiveColumns`); read-only pages may scroll as a whole below their `MinHeight` (Dashboard, Reports). Every page fits the page area of 1366×768 at 125 % (about 820×430 px); below a page's `MinWidth` / `MinHeight` (default 720×400) the whole page scrolls (safety net for even smaller screens).
- **Motion:** short, and respects reduced-motion settings.

## App foundation (B0, shared by the 3 apps)

Everything shared is in `Gym.UI` and `Gym.Client`. An app agent adds files only inside its own project (`src/Gym.Reception`, `src/Gym.Cafeteria`, `src/Gym.Admin`): no change to shared files or to the CI workflow is needed. Ask the coordinator when something shared is missing.

- **API:** pages call `Context.Api` (`Gym.Client.IGymApi`, one method per endpoint; errors throw `GymApiException` with the server `Code`). `GymApiClient` talks to the server. `Gym.Client.Sample.SampleGymApi` answers from memory: 24 members (Arabic and Latin names, placeholder photos, every status: active, expiring, expired, never paid), 2 private trainers with 5 linked clients and their service, the 4 plans, 9 staff (3 club trainers with monthly ratings, 1 inactive), 17 products in Drinks / Shakes / Snacks / Supplements with stock movements (some low, one at 0, one inactive), 7 days of sales and shifts (today's still open), 14 days of check-ins, settings 3 days / $25 / rounding step 1, 30 days of exchange rates up to today's 11,500 SYP for $1 (every third seeded payment and sale in SYP). `SimulateExchangeRate(Stale | None | Today)` switches the rate state for the warning screenshots. It follows the server rules (status from `GetStatus`, stock = sum of movements, renewal chaining) and its writes work in memory, but it checks no roles and is not thread-safe. Logins: `admin`, `sara` (reception), `ahmad` (cashier), password `titanfit`.
- **Live updates:** `Context.Live` (`ILiveConnection`: `GymLiveConnection` keeps reconnecting by itself; `SampleLiveConnection` publishes the sample API's own changes). Events come off the UI thread: wrap handlers in `Dispatcher.InvokeAsync`. The header shows Server online / Connecting / Reconnecting / Server offline.
- **Station settings:** `%ProgramData%\TitanFit\station.json` (`Gym.UI.Station.StationSettings`), camelCase JSON: `serverUrl` (default `http://localhost:5080`), `stationName` (empty = the app default: Reception 1, Cafeteria 1, Admin PC), `language` (`en` / `ar`), `cameraName`, `printerName`, `lastUsername`. No file = first run: the Server settings dialog opens (server address, Test connection = `GET /health`, station name). It is also on the login card and the server button in the header. Pages read `Context.Station` and send `Context.StationName` with check-ins, sales and shifts. A page changes a setting (the printer or camera picker) with `Context.SaveStation(s => s with { PrinterName = name })`: the window keeps it and writes the file (not in the smoke screenshots). The station installer may write this file.
- **Login and roles (`AppRoles`):** Reception app = Reception + Admin, Cafeteria app = Cashier + Admin, Admin app = Admin only; another role gets a clear message. The token stays in memory (`Context.Session`); a 401 on any call (login revoked or expired) goes back to the login with a message. Log out is in the sidebar. Station apps fill in the last username; the Admin app remembers nothing (the admin stays logged out on the server PC). On a server without an admin, the Admin app's login turns into "Create the first admin".
- **Shell:** `StationWindow` (maximized; login, then `AppShell`; content in `UiScaleHost`, see "Screen sizes") with an overlay for the pages: `Context.Ui.Toast(...)`, `ShowError(ex)`, `ShowDialogAsync(new DialogRequest { Title, Message, Content, PrimaryText, OnPrimary })`, `ConfirmAsync(...)`, `ShowMessageAsync(...)`, `RunBusyAsync(...)`. Each page view is built once per login, so its state survives page switches; logout drops it.
- **How to add a page** (or fill a placeholder; every planned screen already has one in `Pages/`):
  1. `src/Gym.<App>/Pages/XxxPage.xaml` with the root `<shell:PageView x:Class="Gym.<App>.Pages.XxxPage" ...>` (copy a placeholder for the xmlns lines) and `public XxxPage(PageContext context) : base(context) { InitializeComponent(); }`.
  2. Load data in `public override async Task OnShownAsync(CancellationToken ct)` (runs each time the page is shown; the screenshot waits for it), not in `Loaded`. Text you set from code: set it again in `public override void OnLanguageChanged()`.
  3. One line in `src/Gym.<App>/<App>App.cs`: `new("xxx", UiIcons.Users, <App>Text.Xxx, context => new XxxPage(context))`, optional `{ Title = <App>Text.XxxTitle }`. The id names the screenshot `<App>-xxx-<lang>.png`.
  4. Hardware (camera, scanner, printer): skip it when `Context.IsSample` (CI has none).
  5. Layout: follow "Screen sizes" (docked main action, scrolling content, no fixed side widths). The smoke run checks the page at 4 screen sizes; a cut-off control fails CI.
- **How to add strings:** `src/Gym.<App>/<App>Text.cs`: `public static LocalText Xxx { get; } = new("English", "العربية");` (`{0}` placeholders in both). XAML: `Text="{ui:Loc {x:Static local:<App>Text.Xxx}}"` (follows the EN / عربي switch); code: `<App>Text.Xxx.For(Context.Language)` or `.Format(Context.Language, args)`. Shared strings: `CommonText`, `ErrorText` (`ErrorText.Describe(ex, language)` turns API errors into staff messages). No visible text outside a strings class: the smoke run fails on an empty or untranslated Arabic text or different placeholders. Money: `controls:MoneyText Amount=...` or `Money.Format` ("150,000 SYP" / "150,000 ل.س"); numbers `NumberText`; dates `DateText` (Syrian months). Do not use `StringFormat` for numbers or dates (the Arabic culture changes the separators). Data that is always left-to-right (phone, URL, codes) gets `FlowDirection="LeftToRight"`; names and other data from code: `BidiText.SetText(textBlock, value)` (left-to-right when it has no Arabic letters). Numbers inside an Arabic sentence: a number in groups or several numbers in a row ("0944 123 456", "#00412 · 0944 123 456") come out reversed, so pass the text through `BidiText.FixNumbers(text, language)` (puts left-to-right marks around every space and sign of each number run and a right-to-left mark after it, like `Money` does for the minus; WPF resolves digits after Arabic letters as Arabic numbers and has no Unicode isolates) or wrap one value with `BidiText.Ltr(value)`. Do not put a URL inside an Arabic sentence (the bidi rules scramble it). Line icons keep their drawing in Arabic; only arrows and chevrons flip.
- **How to add sample data:** the built-in data above covers every planned screen. If a screen needs more, set `SampleSetup = async api => { ... }` in the app's `AppDefinition` and use the normal API calls on the sample (`await api.LoginAsync("ahmad", SampleGymApi.SamplePassword); await api.CreateSaleAsync(...)`); it runs once before the first screenshot. Do not edit `SampleGymApi` from an app. More states of a page (a filled-in form, another result) for the screenshots: `SmokeStates = [new("check-in-expired", "check-in", view => ((CheckInPage)view).ShowSampleAsync(...))]` in the `AppDefinition`; the smoke run shows the page, calls the function with its view and saves `<App>-<id>-<lang>.png`.
- **Shared controls and styles** (all colours are Deep Teal tokens via `DynamicResource`; usage example: `Gym.UI/Shell/ControlsGalleryPage.xaml`, screenshot `<App>-controls-<lang>.png`): buttons `Button.Primary`, `Button.Secondary` (default), `Button.Gold`, `Button.Danger`, `Button.Ghost`, `Button.Icon` with `ui:ThemeAssist.Icon="{x:Static ui:UiIcons.Check}"` (44 px and up); implicit TextBox, PasswordBox, ComboBox, DatePicker and Calendar, CheckBox, ListBox, DataGrid (read-only, 52 px rows, define the columns), ScrollBar, ToolTip; `ui:ThemeAssist.Placeholder` and `ui:ThemeAssist.HasError` on inputs; Border styles `Card`, `Card.Inset`, `Chip`, `Divider`; text styles `Text.H1`, `Text.H2`, `Text.Body`, `Text.Strong`, `Text.Caption`, `Text.Overline`, `Text.Label`, `Text.Number`, `Text.Display`; controls (`xmlns:controls="clr-namespace:Gym.UI.Controls;assembly=Gym.UI"`) `Icon`, `StatusPill` (`Status="{Binding Membership.Status}"`: green / amber / red / grey with icon and label), `MoneyText`, `SearchBox` (`SearchChanged`, 300 ms debounce), `FormField` (`Label`, `Hint`, `Error`), `EmptyState`, `LoadingOverlay`, `Avatar` (+ `Images.FromBytes`; style `Avatar.Large` for a details card, smaller on short windows), `PagePlaceholder`, `ToastView`, `DialogFrame` (body scrolls, buttons docked), `ResponsiveColumns` (up to `Columns` side by side, fewer below `MinColumnWidth`; `controls:ResponsiveColumns.Weight` on a child), `SplitRow` (toolbar: first child at the start, the rest at the end, each on its own line when they do not fit); line icons `UiIcons.*`; breakpoints `ui:Responsive.*`. CommunityToolkit.Mvvm 8.4.2 comes with Gym.UI for view models.
- **Smoke mode:** `Gym.<App>.exe --smoke-screenshots <dir> --lang en|ar [--screen 1366x768@125]` writes `<App>-login`, `-server-settings`, (Admin) `-first-admin`, one per page id, one per smoke state, `-controls` and `-dialog` PNGs, each at the 4 test screens (`SmokeScreen.All`: 1366×768 at 100 % and 125 %, 1920×1080 at 125 % and 100 %; the window content is the screen in logical px minus the taskbar, 48, and the title bar, 32; the PNG has the real pixels, e.g. 1366×668), named `<App>-<page>-<lang>-<WxH>-<scale>.png`, plus `<App>-<lang>.json` (DPI awareness, each screenshot with ok / error, the layout check, text problems, unhandled errors, binding warnings). The layout check (`Gym.UI/LayoutAudit.cs`) runs on every screenshot: every visible button, toggle, text box, combo box and date picker must be inside the window and not cut off by a parent (inside a scrolling area only along an axis that scrolls); primary buttons (`Button.Primary`, `.Gold`, `.Danger`) outside lists must be in view without scrolling (a primary-styled step inside a scrolling form says so with `ui:Responsive.ScrollsWithContent="True"`); lists and tables outside a scrolling area must show at least 44 px. Exit code 1 when a page threw, a layout problem was found, a string failed the check or a UI error was unhandled. Run it locally the same way to see a screen without a server.
- **Self-test mode:** `Gym.<App>.exe --self-test` loads the app's native and critical libraries and prints one line per check (`PASS  name: detail` or `FAIL  name: error`) and a summary; exit code 0 when all pass, 1 otherwise. No window, camera, scanner, printer, server or settings file. Shared checks (`Gym.UI/SelfTest.cs`): the embedded fonts, WPF rendering offscreen (English + Arabic text), per-monitor V2 DPI awareness (`app.manifest`), and at the end the Visual C++ runtime DLLs the process loaded (information). The app's own checks are `AppDefinition.SelfTestChecks`: Reception (`ReceptionSelfTest`): the OpenCV native library (`Cv2.GetVersionString()`), a camera-like BGR frame through OpenCV's grey conversion and ZXing back to the member code, the QRCoder PNG read back by ZXing, the three embedded sounds (read, not played); Admin (`AdminSelfTest`): SkiaSharp, HarfBuzz and a LiveCharts2 column chart drawn offscreen; Cafeteria (`CafeteriaSelfTest`): the receipt drawn in English and Arabic and written as an XPS print document to a temporary file (no printer). On a gym PC: `start /wait "" "C:\Program Files\TitanFit\Reception\Gym.Reception.exe" --self-test` in a command prompt (the lines appear there; `echo %ERRORLEVEL%`), or `& 'C:\Program Files\TitanFit\Reception\Gym.Reception.exe' --self-test | Out-Host` in PowerShell.

## Reception app (step 3)

`src/Gym.Reception`: four pages in the sidebar, built on "App foundation". Tests in `tests/Gym.Tests/Reception`.

- **Check-in** (`check-in`, design `Main`):
  - A member is identified by the webcam (OpenCvSharp, DirectShow; ZXing.Net looks for a QR about 8 times a second; the picture is mirrored like a mirror), by a USB barcode scanner in keyboard (HID) mode (a burst of keys ending with Enter anywhere on the page, read by key position so an Arabic keyboard layout does not matter: `KeyboardWedge`), or by the member number the staff type (`231`, `#00231`, or a typed `TF1-…` code).
  - The same code checks in once while it stays in view, and again after 5 s without it (`CheckInDebouncer`).
  - The big card: photo, name, member number and phone, a status pill and text, plan, start and end date, the days left (or since the end) in big digits, the part of the period used, a note, and a sound. Green: active ("Active · 25 days left"). Amber: expires within the threshold from the settings ("Expires in 2 days"). Red: expired ("Expired 6 days ago") or never paid, with a gold Renew button that opens renew for that member and checks him in after paying; red without Renew for an unknown code (e.g. an old QR after a reissue).
  - Sounds: three generated WAVs embedded in the exe (`Sounds/ok.wav`, `warning.wav`, `denied.wav`), played by Windows.
  - Recent check-ins: the last 8 of today from both desks (live), tap one to show it again; "N entries today".
  - Camera picker (Windows camera names; two of the same get "(2)"), saved as `cameraName` in station.json. The station name goes with every check-in.
  - Fingerprint later: `CheckInPage.CheckInSource` is where a reader plugs in; the server needs a new `CheckInMethod` (today `QrCode` and `MemberNo`).
- **New member** (`new-member`, design `Reception-Register`):
  1. ID card: the USB 2D scanner types the ID barcode into the scan box; 0.4 s after the last key `IIdCardParser` fills the fields, and every field stays editable. The parser is a **placeholder** (`TolerantIdCardParser`): fields split by common separators or spaces, labelled fields in Arabic or English, otherwise the card order (first name, father, surname, mother, birth place), the national number as 11 digits, the birth date as d/m/yyyy, yyyy-m-d or 8 digits, Arabic-Indic digits too. It is replaced when the real scan arrives (open item 1). The raw text stays in the box only; nothing of it is sent or stored.
  2. Phone with +963 in front; a Syrian mobile is stored as `09xxxxxxxx` like the existing members (search finds every form).
  3. Photo: webcam preview, Take photo / Retake, cut to the centred square, 480 × 480 JPEG, uploaded after the member is created. Saving without a photo asks first.
  4. Member type (regular or private trainer), plan (1 / 3 / 6 / 12 months with prices), start today and the end date, cash, total in gold. A private trainer also pays the private training service at once (months × the monthly fee from the settings, a separate payment; it can be unticked).
  - Save creates the member with the first payment, then uploads the photo and pays the service (a failure there is reported; the member stays). Then the member QR is shown: QRCoder (error correction Q) from `MemberDto.QrCode`, the same text check-in reads, with Copy image, Save PNG and Print.
- **Members** (`members`): search by name (Arabic-normalized on the server), phone, national number, member number or QR (typed, a USB scanner anywhere on the page, or the webcam button); filter all / active / expiring / expired. Details: photo, ID data, phone, status, trainer or private service, payments and the check-ins of the last 90 days. Actions: Renew (or the first payment; plan choice, the new dates by the server's renewal rule shown before confirming, total in gold), Show QR (copy, save, print, issue a new one after a warning), Change phone, Change photo.
- **Private trainers** (`private-trainers`): the trainers with their client count and private service ("Paid until …"); pay or renew the service (1, 3, 6 or 12 months × the fee, dates shown first); link a client (search; shows who already trains with another trainer) and unlink; make a member a private trainer, or back to regular (the server refuses while clients are linked: "Unlink the clients first").
- **Live updates:** `MemberChanged` and `CheckInRecorded` from the other desk refresh the page on screen (handlers on the UI thread, 0.4 s debounce); `SettingsChanged` updates the private training fee.
- **Arabic:** numbers in groups go through `BidiText.FixNumbers`; names keep their own direction; the QR image and the camera picture are always left-to-right (a mirrored QR cannot be read).
- **Smoke screenshots:** every page on sample data, plus the states `check-in-expiring`, `check-in-expired`, `new-member-filled`, `new-member-saved` (QR shown), `members-renew`, `members-qr` and `private-trainers-pay`. Sample mode opens no camera, scanner or sound.
- **Hardware assumed:** one USB webcam per reception PC (autofocus, open item 4) and a USB 2D barcode scanner in keyboard (HID) mode with Enter after each code (it reads the ID barcode and, held to a phone, the member QR). The OpenCV native library is inside the exe (OpenCvSharp4.runtime.win). Older OpenCvSharp builds needed the Microsoft Visual C++ 2015-2022 (v14) x64 runtime; the current one links it statically (CI, "Build and release"), and the Station setup installs the runtime with the Reception app anyway (see "Installers"). Without a working camera the card says "Camera not available" and check-in goes on with the scanner and the member number; `Gym.Reception.exe --self-test` then shows whether OpenCV loads.

## Cafeteria app (step 4)

`src/Gym.Cafeteria`, for the 2 cafeteria PCs (Cashier and Admin logins). Four screens, in sidebar order (screenshot id in brackets):

- **Sell** (`cashier`, plus `cashier-paid`), after `design/Cafeteria-POS.dc.html`:
  - Low-stock banner (count + the lowest items), the scan / search box, category tabs from the API with counts, big product tiles: name, price in gold, the stock left after the cart (grey, amber when low, red and disabled when out), a ×N badge for what is in the cart.
  - Order: lines with − / + (− at 1 removes, + stops at the stock), line totals, the current shift (open since, sales, total), cash received with quick amounts (exact, next 5,000 / 10,000 / 50,000) and the change, the total, **Charge** (one `POST /api/sales` with `Context.StationName`; cash only, as in Contracts).
  - `insufficient_stock` (another till sold first): the stock is reloaded, the short lines turn red ("Only 7 in stock") and a dialog lists them (from `shortages` in the answer, else by comparing with the fresh stock). A timeout says to check Receipts before charging again.
  - After a sale: the paid panel (receipt number, change to give, print status, Reprint, New order); the receipt prints at once. Live `StockChanged`, `ProductChanged` and `SaleCompleted` update tiles, cart, banner and shift totals.
- **Stock** (`stock`, plus `stock-adjust`): items with category, barcode, price, stock and minimum, a status pill (in stock / low / out / inactive), low rows tinted; filters: search (name or barcode), category, low only, show inactive. The selected item: stock, minimum, price, **Stock in** (quantity + note), **Adjust** (a change or the counted quantity, reason required; the new stock is shown before saving and a result below zero is refused in the form and by the server, `negative_stock` / `insufficient_stock`), **Edit**, and its movements of the last 90 days. **New item / Edit item:** names EN and AR, category, price, barcode (scan into the field), minimum stock (the low-stock threshold per item), active.
- **Shift close** (`shift-close`, plus `shift-report`): the open shift (or a past one picked in the list) with sales, sales total, opening cash, expected cash and items sold. **Close shift** with the counted cash (missing / extra / matches shown while typing) and a note, confirmed; then the shift (Z) report prints. **Shift report** shows the report as it prints, with Print. No open shift: **Open shift** with the opening cash (a sale also opens one). Past shifts of the last 14 days with totals and status (open / closed / cash short); a cashier sees his own, an admin everyone's.
- **Receipts** (`receipts`): the day's sales (date picker; a cashier sees his own), the selected receipt exactly as it prints (80 mm), Reprint. The station's hardware: the **receipt printer** picker (installed Windows printers or "Windows default printer", saved as `printerName` in station.json through `Context.SaveStation`), Test print (EN + AR sample), and a **barcode scanner test** field.
- **Receipt** (EN or AR, the UI language): TITANFIT, receipt number, date and time, station, cashier, items (name, then qty × price and the line total), total, payment cash, cash received and change, thank-you line. The shift report: shift number, station, cashier, opened / closed, sales, sales total, opening, expected and counted cash, difference, items sold, note, printed time.
- **Code:** view models without WPF (`Sales/Cart.cs`, `Sales/CashierViewModel.cs`, `Sales/ShiftViewModel.cs`, `Inventory/StockViewModel.cs`), `Hardware/BarcodeWedge.cs`, `Receipts/ReceiptLayout.cs` (the slip as rows of text), `Receipts/ReceiptView.cs` (draws the rows for the preview and the printer), `Receipts/ReceiptPrinter.cs` (`IReceiptPrinter`, `WindowsReceiptPrinter`, `PreviewReceiptPrinter`). Unit tests: `tests/Gym.Tests/Cafeteria` (cart math, quick cash rounding, the wedge, receipt rows and view in Arabic, the view models on the sample API).
- **Hardware assumptions (until tested on site):**
  - **Barcode scanner:** USB, keyboard (HID) mode, Enter after each code. On the Sell screen a keyboard wedge on the window takes a burst of 4+ characters at most 60 ms apart followed by Enter as a scan, wherever the focus is (what the scanner typed into a field is taken back); an unknown code gives a warning toast and a beep. Slower typing is normal typing; Enter on a whole barcode in the search box also adds the item. On the other screens a scan simply types into the focused field (item barcode, scanner test).
  - **Receipt printer:** any Windows printer driver (WPF printing: an XPS page with the receipt drawing). Paper narrower than 100 mm counts as a roll: the page is as long as the receipt, 72 mm printable width; on an A4 printer the receipt sits at the top of the page. ESC/POS (raw printing, cutter and cash drawer commands, logo) waits for the printer model (open item 2): a new `IReceiptPrinter` can print the same `ReceiptLayout` rows.
  - Low-stock warning: the shell has no badge on the sidebar items, so the Sell screen shows a banner and the Stock screen a count.
  - Smoke screenshots: no printer and no scanner are touched (`Context.IsSample`); the Sell screen shows a filled cart with cash typed in.

## Admin app (step 5)

The Admin app (`src/Gym.Admin`, Admin role only) has these pages; each one is in the smoke screenshots (`Admin-<page>-en|ar.png`).
- **Dashboard** (`GET /api/dashboard` + this month's reports + backup status): today's check-ins (busiest hour), active / expiring / expired members, revenue today (subscriptions and cafeteria) and this month (memberships, private training, cafeteria), check-ins by hour (LiveCharts2 column chart, SkiaSharp, theme colours), members expiring within the threshold, low stock. A banner warns when the last backup failed or is older than 2 days. It refreshes on the live events (check-ins, members, sales, stock, products, settings) and once a minute.
- **Members:** search (name, phone, national number, member number), status filter (all, active, expiring, expired), type filter (regular, private trainers), pages of 20; the chosen member's status line, details, private trainer service and linked clients (or his trainer), payments and the check-ins of the last 30 days. Edit changes the member data (names, birth, national number, phone, type, notes); renewals, photos, QR codes and trainer links stay in the Reception app. No delete, no refund.
- **Plans & prices:** list, add and edit (months 1 to 120, price, active). A new price applies to new payments only.
- **Cafeteria items:** search, category filter, inactive items on demand; add and edit (names EN / AR, category, price, barcode, low-stock minimum, active); "Stock" takes in a delivery or sets the counted quantity (stocktake). The stock never goes below 0.
- **Staff & ratings:** staff list (job, login and role, phone, active); add and edit (job type, optional login with username, role and password; a club trainer with a login needs an explicit role); reset password. The server refuses to remove the last admin (`last_admin`) and signs a person out on every PC when his login, role, password or active flag changes. Club trainers get one rating a month (1 to 5 stars + note); rating a month again replaces it; the history stays.
- **Settings:** the expiring-soon threshold (0 to 60 days) and the private training monthly fee, with a worked example; the server version (`/health`), address, this PC and the signed-in admin; a summary of the daily backup.
- **Reports:** subscriptions, attendance or sales for today, this week (from Saturday), this month, last month or custom dates: totals and tables; Export CSV (UTF-8 with BOM, so Excel shows Arabic) and Print.
- **Backups:** see "Backups".
- **First admin:** the server setup wizard creates the first admin (`POST /api/setup/admin`). If it was skipped, the Admin app's login shows "Create the first admin" instead; when the admin was created elsewhere meanwhile, the form goes back to sign-in.
- Logic with unit tests is in `src/Gym.Admin/Logic` (report periods and tables, CSV, rating months, form checks); `tests/Gym.Tests` references the Admin app.

## Backups (step 5)

- **Daily job:** the server (`BackupScheduler` in Gym.Api) checks every minute; at the daily time (default **02:00**, server PC local time) it runs `pg_dump --format=custom` with the server's own connection (`server.json`; the password goes in `PGPASSWORD`, never on the command line). A run that was missed while the PC was off happens as soon as the server is back. A new install waits for the first 02:00. "Back up now" in the Admin app runs the same backup at once.
- **pg_dump:** the one installed with the server (`C:\Program Files\TitanFit\pgsql\bin\pg_dump.exe`, found next to `Server\`), else the path in the backup settings or `Backup:PgDumpPath`, else the PATH. It must be the server's major version (17).
- **Folder:** `%ProgramData%\TitanFit\backups` by default (created by the server setup for SYSTEM and Administrators only: the dumps hold member data). For the external drive (open item 8) choose a folder on the Backups page; the server checks that it exists and that it can write there. When the drive is unplugged the run fails with `backup_folder_missing` and the warning shows.
- **Files and retention:** `titanfit-YYYYMMDD-HHmm.dump` (`-2`, `-3` for a second one in the same minute). After a good backup only the newest N stay (setting, default **30**, 1 to 365). Nothing else in the folder is ever deleted (other files, `.partial` leftovers, the `titanfit-before-restore-*` copies).
- **Settings and results:** `%ProgramData%\TitanFit\backup-settings.json` and `backup-state.json` (not in the database, so they survive a restore); each run is also logged to `%ProgramData%\TitanFit\logs\backup.log` and the server log.
- **Warning:** the dashboard and the Backups page warn when the last run failed, when the last good backup is older than **2 days**, or when there is none yet.
- **Restore (on the server PC, never from the app):**
  1. Pick the file (Backups page list, or the folder on the drive).
  2. Open PowerShell **as administrator** and run `& 'C:\Program Files\TitanFit\Setup\restore-backup.ps1' -BackupFile 'E:\TitanFit-Backups\titanfit-20261127-0200.dump'`, then type `YES`.
  3. The script checks the file (`pg_restore --list`), saves the current data as `%ProgramData%\TitanFit\backups\titanfit-before-restore-<time>.dump`, stops the TitanFit Server service, drops and recreates the `titanfit` database, restores it with `pg_restore --no-owner --role=titanfit` as the PostgreSQL superuser from `database-admin.json`, starts the service (newer migrations apply) and waits for `/health`. Log: `%ProgramData%\TitanFit\logs\restore.log`.
  4. Everything in the database goes back to the time of the backup (members, payments, check-ins, staff and logins, products, stock, sales). Photos, keys and settings files stay as they are.
  - By hand, without the script: stop `TitanFitServer`, then with the superuser password from `database-admin.json`: `psql -h localhost -p 5433 -U postgres -d postgres -c "DROP DATABASE titanfit WITH (FORCE)"`, `... -c "CREATE DATABASE titanfit OWNER titanfit"`, `pg_restore -h localhost -p 5433 -U postgres -d titanfit --no-owner --role=titanfit <file>`, start `TitanFitServer`.
- **Tests:** unit tests (names, retention, schedule with a fake clock, warning, settings checks); integration tests with the real pg_dump 17 (the Linux job installs `postgresql-client-17` from the PGDG apt repo); the install smoke test runs "Back up now" on the real Windows install, checks the file and its ACL, reads it with `pg_restore --list`, then restores it with `restore-backup.ps1` and checks that later changes are gone.

## Security

- The QR holds no personal data (random code only).
- Role-based access on every API endpoint.
- Passwords are stored hashed.
- Data stays on the LAN. Daily backups (see "Backups"; the backup folder is for SYSTEM and Administrators only).
- Never commit passwords, connection strings with secrets, or member data. **This repo is public.**

## API overview (step 2.4)

- **Server:** `Gym.Api`, `http://<admin-pc>:5080` (setting `Urls`). Runs as the Windows service `TitanFitServer` ("TitanFit Server", `UseWindowsService`) or as a console app for development. Swagger UI (`/swagger`) and `/openapi/v1.json` in Development only.
- **Configuration** (appsettings.json, then `%ProgramData%\TitanFit\server.json` written by the server setup, then environment variables such as `ConnectionStrings__Gym`; nothing secret is committed):
  - `ConnectionStrings:Gym`: PostgreSQL connection string.
  - `Database:MigrateOnStartup` (default true): apply migrations and default data at start.
  - `Storage:DataPath` (default `%ProgramData%\TitanFit`): member photos and the token signing key.
  - `Jwt:Key` (optional, 32+ bytes): otherwise a random key is created once in `{DataPath}/keys`. `Jwt:LifetimeHours` (default 12).
- **Login:** `POST /api/auth/login` returns a JWT (staff id, name, role). Send it as `Authorization: Bearer`. A token stops working at once when the staff member is deactivated or his password, username or role changes.
- **First run:** `GET /api/setup/status` and `POST /api/setup/admin` create the first admin; refused once an admin exists.
- **Errors:** RFC 7807 problem details with a stable `code` (e.g. `insufficient_stock`, `duplicate_national_no`, `not_a_private_trainer`): 400 rule or input, 401 not logged in, 403 role, 404 not found, 409 conflict.
- **Live updates:** SignalR hub `/hubs/gym` (any logged-in staff) pushes `CheckInRecorded`, `MemberChanged`, `StockChanged`, `ProductChanged`, `SaleCompleted`, `SettingsChanged`, `ExchangeRateChanged`. `Gym.Client.GymLiveConnection` wraps it (auto-reconnect).
- **Client:** `Gym.Client.IGymApi` has one method per endpoint below; `GymApiClient` implements it for the server, `Sample.SampleGymApi` in memory (see "App foundation").

Roles: **A** = Admin, **R** = Reception, **C** = Cashier. Admin can call everything.

| Endpoint | Roles | What |
|---|---|---|
| `GET /health` | anyone | status and version |
| `POST /api/auth/login` | anyone | log in → token |
| `GET /api/auth/me`, `POST /api/auth/change-password` | A R C | own account |
| `GET /api/setup/status`, `POST /api/setup/admin` | anyone (first run only) | create the first admin |
| `GET /api/members?q=&status=&type=&page=&pageSize=` | A R | search by name (Arabic-normalized), phone, national no, member no or QR |
| `GET/PUT /api/members/{id}`, `POST /api/members` | A R | get, update, register (optional `planId` pays the first month(s)) |
| `PUT/GET /api/members/{id}/photo` | A R | photo upload (raw image/jpeg or image/png, max 5 MB) and download |
| `POST /api/members/{id}/renew` | A R | renew with a plan (early renewal rule) |
| `POST /api/members/{id}/qr-code` | A R | reissue the QR code (old one stops working) |
| `GET /api/members/{id}/subscriptions` | A R | payment history |
| `POST /api/check-ins`, `GET /api/check-ins?from=&to=&memberId=` | A R | check in by QR code or member no (logged with station) → status, days, photo URL; check-in log |
| `GET /api/plans` | A R C | plans and prices |
| `POST /api/plans`, `PUT /api/plans/{id}` | A | edit plans and prices (deactivate with `isActive`) |
| `GET /api/private-trainers`, `GET /api/private-trainers/{id}/clients` | A R | trainers with client count and service status; their clients |
| `PUT/DELETE /api/private-trainers/{id}/clients/{memberId}` | A R | link / unlink a client |
| `POST /api/private-trainers/{id}/service` | A R | buy or renew the private training service (months × fee) |
| `GET/POST /api/staff`, `GET/PUT /api/staff/{id}` | A | staff, type, optional login, active |
| `POST /api/staff/{id}/password` | A | reset a password |
| `GET /api/staff/{id}/ratings`, `PUT /api/staff/{id}/ratings/{year}/{month}`, `GET /api/staff/ratings?year=&month=` | A | club trainer ratings and history |
| `GET /api/product-categories`, `GET /api/products?q=&categoryId=`, `GET /api/products/{id}`, `GET /api/products/by-barcode/{code}` | A R C | categories, products with current stock and low-stock flag |
| `POST/PUT /api/product-categories`, `POST/PUT /api/products` | A C | edit categories and products |
| `GET /api/stock?lowOnly=`, `POST /api/stock/in`, `POST /api/stock/adjustments`, `GET /api/stock/movements` | A C | stock levels, stock-in, adjustments (delta or counted), history |
| `POST /api/sales`, `GET /api/sales/{id}`, `GET /api/sales?from=&to=&cashierId=` | A C | sell (one transaction) → receipt; a cashier lists his own sales |
| `POST /api/shifts/open`, `GET /api/shifts/current`, `POST /api/shifts/{id}/close`, `GET /api/shifts/{id}/summary`, `GET /api/shifts` | A C | shifts and the summary per cashier and shift (a cashier sees his own) |
| `GET /api/dashboard` | A | today's check-ins by hour, active / expiring / expired counts, revenue (subscriptions, private training, cafeteria), low stock, expiring list |
| `GET /api/reports/subscriptions`, `/attendance`, `/sales` `?from=&to=` | A | reports over local days, both ends included |
| `GET /api/settings` | A R C | expiring-soon threshold, private training fee (USD), SYP rounding step |
| `PUT /api/settings` | A | change the settings |
| `GET /api/exchange-rates/current` | A R C | the rate in use (newest), its state (today / stale / none), the rounding step |
| `GET /api/exchange-rates?from=&to=`, `POST /api/exchange-rates` | A | rate history (newest first); enter today's rate (SYP for $1) |
| `GET /api/backups`, `GET /api/backups/status`, `POST /api/backups` | A | backup files (newest first); last run, last good backup, next run, warning; back up now (a failed run answers `succeeded: false` + `errorCode`; 409 `backup_running`) |
| `GET/PUT /api/backups/settings` | A | backup folder (must exist and be writable: `backup_folder_missing`, `backup_folder_not_writable`), daily time, how many to keep, pg_dump path |
| `/hubs/gym` (SignalR) | A R C | live updates |

## Build and release (GitHub Actions)

The workflow file is `.github/workflows/build.yml`. It is added in step 2.1.

- **Triggers:** push and pull request to `master`, and tags `v*`.
- **Job on `windows-latest`:**
  1. Set up .NET 10, then restore, build (Release) and test.
  2. Publish `Gym.Api`, `Gym.Admin`, `Gym.Reception`, `Gym.Cafeteria` as standalone exes: `-r win-x64 --self-contained -p:PublishSingleFile=true` (WPF apps also `-p:IncludeNativeLibrariesForSelfExtract=true`). The gym PCs need no .NET install.
  3. Self-test: each published exe runs with `--self-test` (see "App foundation"), its bundle unpacked into `bundle-extract/`. Then **native dependencies**: `dumpbin /dependents` on each exe and every native file its bundle unpacks shows which app needs the Microsoft Visual C++ runtime (`msvcp140*.dll`, `vcruntime140*.dll`, ...; WPF's own `vcruntime140_cor3.dll` does not count). The step lists the imports of each file. Today no app imports it: `OpenCvSharpExtern.dll` (OpenCvSharp4.runtime.win 4.13.0.20261107), `libSkiaSharp.dll` and `libHarfBuzzSharp.dll` link the C runtime statically, and the WPF files use `vcruntime140_cor3.dll`. The Station setup installs the runtime with the Reception app anyway (a safety net for an OpenCV build that needs it). An app whose setup does not install the runtime (Admin, Cafeteria) fails the step as soon as one of its native files imports it.
  4. Smoke test: each WPF exe runs with `--smoke-screenshots <dir> --lang en|ar` on the in-memory sample data (no server). It renders the login, the server settings, every registered page, the control gallery and a dialog offscreen at the 4 test screens (1366×768 at 100 / 125 %, 1920×1080 at 125 / 100 %, rendered at that DPI), runs the layout check on each (see "Smoke mode"), checks the embedded fonts and every EN/AR string, writes `<App>-<page>-<lang>-<screen>.png` plus a manifest `<App>-<lang>.json`, and exits non-zero when anything failed. CI checks every PNG in the manifests (exists, > 10 KB), that the apps run PerMonitorV2, lists every layout problem and prints a table per app, language and screen (screenshots, controls checked, primary buttons, problems), so a new page needs no workflow change.
- **Job `installers`** (`windows-latest`, after the Windows job): `installer/build-installers.ps1` builds both setups (see "Installers") from the `titanfit-exes` artifact.
  - The runner has an Inno Setup 6 preinstalled (version not readable); the script installs the pinned 6.7.4 (SHA-256 checked) so the build does not depend on the runner image. PostgreSQL and Inno Setup are pinned in `installer/downloads.json` and cached with `actions/cache`.
  - The Visual C++ redistributable for the Station setup (`vc_redist.x64.exe`) comes from Microsoft's permalink `https://aka.ms/vc14/vc_redist.x64.exe` (in `downloads.json`), cached per month (`vcredist-x64-<yyyy-MM>`). Microsoft publishes no fixed hash for it, so `build-installers.ps1` accepts it only with a valid Authenticode signature whose signer is Microsoft Corporation, the product name of the x64 Visual C++ redistributable and a 14.x version (checked on every build, cached or not); otherwise the build fails. Its version goes into the Station setup (`/DVCRedistVersion`).
  - Version: `<Version>` in `Directory.Build.props`. On a tag, the tag must be `v<Version>` (or `v<Version>-suffix`, a pre-release), otherwise the job fails.
- **Job `install-smoke`** (fresh `windows-latest`): `installer/tests/install-smoke.ps1` installs the Server setup silently, checks both services, the ports, the ACLs, the firewall rule, `/health`, the first admin (`POST /api/setup/admin`), login, a `pg_dump` backup, "Back up now" through the API (file, ACL, `pg_restore --list`) and `restore-backup.ps1`; runs the setup again (upgrade); runs the installed Admin app's `--self-test`; installs the Station setup with `/SERVERURL` (and checks that a bad URL is refused), checks the Visual C++ runtime step in its log (vc_redist ran with exit code 0, 1638 or 3010, or was skipped because the installed runtime is at least the bundled version) and the runtime in the registry, and runs the installed Reception and Cafeteria apps' `--self-test`; uninstalls both (services and rule gone, `%ProgramData%\TitanFit` kept); reinstalls the server on the kept data.
- **Job `release`** (tags `v*` only, after both): creates the GitHub Release (or updates its files) with the two setups and `SHA256SUMS.txt`, using the workflow's own token (`contents: write` on this job only).
- **Outputs:**
  - Every run uploads the artifacts `titanfit-exes` (the 4 exes), `screenshots` (EN + AR PNG of every screen per app at the 4 test screens, with the manifests), `test-results` and `integration-test-results` (trx), `titanfit-installers` (the 2 setups + `SHA256SUMS.txt`) and `install-logs` (setup logs, server-setup.log, PostgreSQL log, events).
  - A `v*` tag also attaches the setups to a GitHub Release.
- **Database tests:** GitHub service containers work only on Linux runners. The `integration` job (`ubuntu-latest`, `postgres:17` service) runs `tests/Gym.IntegrationTests` (database + API tests, each test class on its own new database) and uploads `integration-test-results`. The Windows job runs only `tests/Gym.Tests`.
- **Migrations:** no .NET SDK is needed locally. Run the `ef-migration` workflow (Actions tab, input: migration name), download the `ef-migration` artifact and commit the files in `src/Gym.Infrastructure/Persistence/Migrations`. The integration tests fail if the model and the migrations differ.
- **Fetching the files back:**
  ```
  gh run download <run-id> -R mostafahasan1995/titanfit-gym
  gh release download v0.1.0 -R mostafahasan1995/titanfit-gym
  ```
- **Unsigned exes:** Windows SmartScreen shows a warning (More info → Run anyway). A code-signing certificate can be added later.

## Installers (step 2.5)

Two Inno Setup 6 setups, built in CI (job `installers`), x64, admin rights, English and Arabic wizard, TITANFIT images and icon rendered from `Gym.UI` (logo + Alexandria). Sources: `installer/server.iss`, `installer/station.iss`, `installer/common.iss`, `installer/scripts/*.ps1`.

**`TitanFit-Server-Setup-<version>.exe`** (admin PC, about 104 MB):
- Installs to `C:\Program Files\TitanFit`: `Server\Gym.Api.exe` + `appsettings.json`, `pgsql\` (PostgreSQL 17.12 from the EDB Windows x64 binaries zip: bin, lib, share, incl. `pg_dump.exe` / `pg_restore.exe`; no pgAdmin; the Visual C++ runtime DLLs are copied next to it), and optionally `Admin\Gym.Admin.exe` (component, on by default; Start menu + desktop shortcuts).
- **Database (first install only):** `initdb` in `%ProgramData%\TitanFit\pgdata` (UTF8, built-in `C.UTF-8` locale, data checksums, scram-sha-256), `listen_addresses = 'localhost'`, **port 5433** (no clash with another PostgreSQL). Service **`TitanFitDatabase`** ("TitanFit Database (PostgreSQL 17)", NetworkService, automatic, restart on failure). A `titanfit` role owns the `titanfit` database. Both passwords are random (32 characters) and never leave the PC. The data folder is readable only by SYSTEM, Administrators and the database service.
- **Config:** `%ProgramData%\TitanFit\server.json` (connection string, data path) and `database-admin.json` (PostgreSQL superuser), both readable only by SYSTEM and Administrators. The `keys` folder (token signing key) and the `backups` folder (default target of the daily backup) are locked the same way. `Setup\restore-backup.ps1` restores a backup (see "Backups").
- **Server service:** **`TitanFitServer`** ("TitanFit Server", LocalSystem, automatic, depends on the database service, restarts after 5 s / 15 s / 60 s). Migrations run when it starts. The setup registers the `Gym.Api` event log source (Application log) for the server's warnings and errors; left to the service, creating it at the first logged error (EF's expected "no migrations table yet" on a fresh database) was once refused and that logging error stopped the server.
- **Firewall:** inbound TCP **5080**, remote address **LocalSubnet** only, **Private and Domain** profiles (rule `TitanFit-Server-TCP-5080`). The gym network of the server PC must be set to Private, or the stations cannot connect.
- **End of the wizard:** the setup waits (up to 3 minutes) until `http://localhost:5080/health` answers, then shows a **"First administrator"** page that calls `POST /api/setup/admin` (only while no admin exists; it can be skipped and comes back on the next run), then the server address for the stations (`http://<PC name>:5080`). If the server does not start, it says so and points to `%ProgramData%\TitanFit\logs\server-setup.log`.
- **Upgrade:** run the newer setup. It stops both services, replaces the files, keeps `server.json`, the database and the data, then starts the services (the server applies new migrations) and waits for `/health`. A different PostgreSQL major version is refused (needs a manual upgrade).
- **Uninstall:** stops and removes both services and the firewall rule, removes the program files. It **keeps `%ProgramData%\TitanFit`** (database, photos, keys, settings, logs) and says so on the confirm screen. Installing again reuses that data (same logins).
- **Silent:** `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART [/COMPONENTS="server"] [/LOG=file]`. Exit code 20 = files installed but the server did not start. Log: `%ProgramData%\TitanFit\logs\server-setup.log` (never contains a password).

**`TitanFit-Station-Setup-<version>.exe`** (reception and cafeteria PCs, about 167 MB: all three apps and the Visual C++ redistributable are inside, only the chosen apps are installed):
- Components: Reception, Cafeteria, Admin (none preselected; at least one). Each goes to `C:\Program Files\TitanFit\<App>\` with Start menu + desktop shortcuts.
- **Visual C++ runtime (Reception camera):** for OpenCV, as a safety net (older OpenCvSharp builds needed the Microsoft Visual C++ v14, 2015-2022 and later, x64 runtime; the current one does not import it). With the Reception app chosen, the setup reads `HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64` (32-bit view, where the redistributable registers it, and the 64-bit view): `Installed` = 1 and the version from `Major`.`Minor`.`Bld`.`Rbld` (or `Version`). Missing or older than the bundled `vc_redist.x64.exe`: it runs `vc_redist.x64.exe /install /quiet /norestart` (log `%ProgramData%\TitanFit\logs\vc_redist-x64.log`). Exit codes 0, 1638 (a newer one is installed) and 3010 (installed, restart needed: the last page asks to restart Windows) are success; another code shows a warning, the apps stay installed, and a silent install ends with exit code 22. The setup log has one line per decision (`VC++ runtime x64: installed …, bundled …: up to date, step skipped.` or `…: running vc_redist.x64.exe …` and its exit code). The uninstaller leaves the runtime (other programs share it). The Cafeteria and Admin apps import no Visual C++ runtime (checked in CI, "Build and release"), so the Server setup does not install it (its PostgreSQL has the DLLs next to it).
- Asks for the server address (default `http://SERVER-PC:5080`, or the one already in `station.json`), checks the form (`http(s)://host[:port]`, no path) and tries `/health` (a server that does not answer only gives a warning).
- Writes `%ProgramData%\TitanFit\station.json`: `{ "serverUrl": "http://SERVER-PC:5080" }` (other fields in the file are kept). The server setup writes `http://localhost:5080` there for the Admin app if the file does not exist.
- Uninstall removes the apps and shortcuts and keeps `station.json`.
- **Silent:** `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /COMPONENTS="reception,cafeteria" /SERVERURL=http://SERVER-PC:5080`. Exit code 1 = invalid `/SERVERURL`, 7 = no app chosen, 21 = `station.json` not written, 22 = the Visual C++ runtime could not be installed.

**Why the binaries zip and not the EDB installer:** the zip is checksum-pinned and unpacks to our own folder, port and service, so it cannot clash with another PostgreSQL on the PC, upgrades with our files, and keeps the setup small (no pgAdmin / StackBuilder, about 35 MB compressed instead of about 360 MB). The EDB installer would add its own uninstall entry, a default service on port 5432 and its own failure modes in unattended mode.

**Cutting a release:**
1. Set `<Version>` in `Directory.Build.props` (e.g. `0.2.0`), commit and push to `master`.
2. Tag that commit and push the tag: `git tag v0.2.0 && git push origin v0.2.0` (`v0.2.0-rc1` gives a pre-release).
3. The `build` workflow builds, tests, runs the install smoke test and creates the GitHub Release with both setups and `SHA256SUMS.txt`. Download: `gh release download v0.2.0 -R mostafahasan1995/titanfit-gym`.

## First install at the gym

**English**
1. **Server PC network = Private:** on the admin PC, Windows Settings > Network & internet > the gym network > Network profile type **Private**. Otherwise the stations cannot reach the server.
2. **Server setup:** run `TitanFit-Server-Setup-<version>.exe` on the admin PC, wait for the server to start, **create the first admin** on the "First administrator" page, and write down the server address it shows (`http://<PC name>:5080`).
3. **Backup folder:** plug in the external drive, open the Admin app > Backups, choose a folder on that drive (e.g. `E:\TitanFit-Backups`), then "Back up now" once and check the file is there.
4. **Station setup on each PC:** on every reception and cafeteria PC run `TitanFit-Station-Setup-<version>.exe`, choose the apps and enter **the server address** from step 2. With the Reception app it also installs the Microsoft Visual C++ runtime for the camera; restart Windows if it asks.
5. **SmartScreen:** "Windows protected your PC" appears because the setups are not code-signed yet: click **More info > Run anyway**.
6. **Test the hardware:** Reception: the camera picture, a member QR on a phone at check-in, the ID scanner in the New member scan box. Cafeteria: a product barcode on the Sell screen, Receipts > Test print on the receipt printer. If the camera says "Camera not available", run the Reception self-test (see "App foundation", Self-test mode) and send its lines.

**بالعربية**
1. **شبكة جهاز الخادم = خاصة (Private):** على جهاز الإدارة: إعدادات Windows > الشبكة والإنترنت > شبكة النادي > نوع ملف تعريف الشبكة **خاص**. بدون ذلك لا تصل المحطات إلى الخادم.
2. **إعداد الخادم:** شغّل `TitanFit-Server-Setup-<version>.exe` على جهاز الإدارة، انتظر حتى يعمل الخادم، **أنشئ المدير الأول** في صفحة "المدير الأول"، واكتب عنوان الخادم الظاهر (`http://<اسم الجهاز>:5080`).
3. **مجلد النسخ الاحتياطي:** صِل القرص الخارجي، افتح تطبيق الإدارة > النسخ الاحتياطي، اختر مجلدًا على هذا القرص (مثل `E:\TitanFit-Backups`)، ثم "نسخ احتياطي الآن" مرة واحدة وتأكد من وجود الملف.
4. **إعداد المحطة على كل جهاز:** على كل جهاز استقبال وكافتيريا شغّل `TitanFit-Station-Setup-<version>.exe`، اختر التطبيقات وأدخل **عنوان الخادم** من الخطوة 2. مع تطبيق الاستقبال يثبّت أيضًا مكتبة Microsoft Visual C++ اللازمة للكاميرا؛ أعد تشغيل Windows إذا طلب ذلك.
5. **SmartScreen:** تظهر رسالة "Windows protected your PC" لأن ملفات الإعداد غير موقّعة بعد: اضغط **More info ثم Run anyway**.
6. **اختبار الأجهزة:** الاستقبال: صورة الكاميرا، رمز QR لعضو على الهاتف في "تسجيل الحضور"، قارئ الهوية في خانة المسح في صفحة "مشترك جديد". الكافتيريا: باركود منتج في شاشة البيع، و"الإيصالات > طباعة تجريبية" على طابعة الإيصالات. إذا ظهرت "الكاميرا غير متاحة" شغّل الفحص الذاتي لتطبيق الاستقبال (`--self-test`) وأرسل الأسطر الناتجة.

## Steps

1. **Design mockups.** ✅ Done.
2. **Core**, in small parts, each with tests and real test output:
   1. Solution skeleton (all projects, empty TITANFIT windows) + build workflow → first exes from Actions. ✅ Done: `TitanFit.slnx`, `GET /health` + 3 tests, `Gym.UI` theme (Deep Teal, fonts, logo, shared app shell with EN / عربي), 3 WPF shells, CI with exes and EN/AR screenshots.
   2. Domain: subscription dates and `GetStatus` + unit tests. ✅ Done.
   3. Database: EF Core model + first migration + PostgreSQL integration tests. ✅ Done.
   4. API: endpoints, SignalR hub, staff login with roles + tests. ✅ Done (see "API overview"). CI: 165 unit tests (Windows job) and 49 PostgreSQL/API integration tests (Linux job).
   5. Installers (Inno Setup) in CI + GitHub Release on tags. ✅ Done (see "Installers"): server and station setups, install smoke test on a Windows runner.
3. **Reception app.** ✅ Done (see "Reception app"): QR check-in with the webcam, USB scanner and member number; new member with ID scan, photo, plan and QR (copy, save, print); search, details and renew; private trainers. The ID barcode parser waits for a real scan (open item 1).
4. **Cafeteria app:** cashier screen, stock, low-stock warnings, receipts, shift close. ✅ Done (see "Cafeteria app").
6. **Money in US dollars + daily exchange rate** (owner, 2026-11-28). ✅ Done (see "Money and the exchange rate").
5. **Admin app and deployment:**
   - Dashboard, management screens, reports, backups. ✅ Done (see "Admin app" and "Backups").
   - Final installers and release v0.1.0 (Visual C++ runtime for the camera, `--self-test` in the three apps, checked by the install smoke test). ✅ Done.
   - On-site hardware test. Waiting (on site, "First install at the gym").

## Data model (as built in step 2.3)

PostgreSQL, snake_case names, money in US dollars (`numeric(14,2)`, since migration `UsdAndExchangeRates`; before: whole SYP `bigint`), timestamps in UTC, days as `date` (the server's local time zone decides "today").

- `members`: id (uuid), member_no (unique, from the sequence `member_numbers`; shown as #00412), type (Regular / PrivateTrainer), first name, father's name, surname, mother's name, birth date, birth place, national_no (unique when present), phone (stored without spaces), notes, photo_file, qr_code (unique, random `TF1-` + 24 characters), private_trainer_id (→ members, nullable), search_name (normalized names), created/updated at.
- `plans`: months, price, active.
- `subscriptions`: member, kind (Membership / PrivateTraining), plan (null for the private service), months, start_date, end_date (inclusive), price_paid (USD), currency (Usd / Syp), paid_amount (in that currency), exchange_rate (SYP for $1, nullable), created_by (→ staff), created_at.
- `check_ins`: member (null when the code matched nobody), at, station, method (QrCode / MemberNo), scanned_value, result (Active / ExpiringSoon / Expired / NoSubscription / UnknownMember), days left / overdue, staff.
- `staff`: full name, phone, type (Admin / Reception / Cashier / ClubTrainer), active, username (unique, optional), password_hash (ASP.NET Identity PBKDF2), role (optional), security_stamp, created_at, last_login_at.
- `staff_ratings`: staff, year, month (unique per staff and month), stars (1-5), note, rated_by, created/updated at.
- `product_categories`: name EN/AR, sort order, active (seeded: Drinks, Shakes, Snacks, Supplements).
- `products`: name EN/AR, category, barcode (unique when present), price, min_stock, active.
- `stock_movements`: product, quantity (+/−), type (StockIn / Sale / Adjustment), sale, note, staff, at. Current stock = sum.
- `sales` + `sale_lines`: number (receipt, from the sequence `sale_numbers`), station, cashier, shift, at, total (USD), currency, paid_amount (in that currency), exchange_rate, cash_received (in that currency); product, quantity, unit price, line total (USD).
- `shifts`: cashier, station, opened_at, closed_at, opening_cash, closing_cash, note. One open shift per cashier (partial unique index).
- `settings`: one row: expiring_soon_days (3), private_training_monthly_fee ($25), syp_rounding_step (1).
- `exchange_rates`: date (local day), syp_per_usd (> 0), entered_by (→ staff), entered_at. The newest row is the rate in use.
- Check constraints guard months ≥ 1, end after start, prices ≥ 0, stars 1-5, sale quantity > 0.
- **Biometrics (later):** not built. A separate `member_biometrics` table (member, device, template reference) and a new check-in method (`Fingerprint`, `Face`) will be added; nothing in the model blocks it.
- First migration: `InitialCreate`. The API applies migrations at start and adds the default plans and categories once. There is no built-in password: the first admin is created through the first-run setup.

## Incoming requirements (from the owner)

Answers received during step 2.1. Private trainers, staff types and ratings are built in the data model and the API (steps 2.2 to 2.4); the screens come with the apps. Biometrics are not built yet (room left in the model).

- **Private trainer:**
  - Not staff. A private trainer is a member (player) who pays the normal member subscription.
  - If he trains clients at the gym, he also pays a "private training service" subscription.
  - That fee is one fixed amount, set in the system settings, the same for all private trainers.
  - His clients are linked to him in the system. Each client is a normal member with their own subscription.
- **Staff types:** Club trainer (a gym employee), Reception, Cashier, Admin.
  - Club trainers appear in the Staff section.
  - Club trainers get a monthly rating from management: 1 to 5 stars plus a note, with history.
- **Biometric identification:**
  - Start with a USB fingerprint reader at reception (e.g. ZKTeco ZK9500; confirm the model before coding).
  - QR stays as the fallback.
  - Face recognition later, through a door terminal.

## Open items

1. **Syrian ID barcode:** what it actually holds is still unknown. The Reception app ships a tolerant placeholder parser (`TolerantIdCardParser`, see "Reception app") and every field can be typed. The owner scans one ID into Notepad and sends the text (digits may be masked); then the parser gets the real layout and a test. Also check how the scanner types the Arabic names (keyboard layout or Alt codes).
2. **Receipt printer model:** still unknown. The Cafeteria app prints through any Windows driver for now (80 mm layout, see "Cafeteria app"). With the model: test width, cut and paper feed on site, and decide on ESC/POS (raw printing, cash drawer kick, logo).
3. **Touchscreens:** not bought yet. The UI already uses touch-size targets.
4. **Webcams:** buy autofocus models; fixed-focus ones struggle with a QR on a phone screen. Decide one or two per reception PC; a second keeps check-in running during registration.
5. ~~**Early renewal**~~ decided: the new period starts the day after the old end date (see "Business rules").
6. **Expiring threshold:** 3 days, a setting the admin can change (`PUT /api/settings`).
7. **Arabic digits:** 0-9 now. It can be switched to ٠-٩.
8. **Backup target:** which external drive (letter and folder). Until it is set on the Backups page, the daily backup goes to `%ProgramData%\TitanFit\backups` on the same disk as the database, which does not survive a disk failure. Best: a USB drive that stays connected, plus a second copy taken home now and then.
9. **Repo is public:** the owner is considering making it private.
10. **Fingerprint reader model:** confirm before coding (e.g. ZKTeco ZK9500).
11. **Negative stock:** refused for now (see "Business rules"). The owner can ask to allow selling with a warning instead.
12. **HTTPS on the LAN:** still plain HTTP: the API listens on `http://0.0.0.0:5080`, and the firewall rule lets only the local subnet in (Private and Domain networks). Logins and data cross the gym LAN unencrypted. HTTPS (a local certificate the stations trust, installed by the setups) comes later.
13. **Barcode scanner on site:** check that it is in USB keyboard mode with Enter after the code, and that it types fast enough for the Sell screen (at most 60 ms between characters; otherwise change `BarcodeWedge.DefaultMaxGapMs`). Also check the barcodes on the real products.
14. **Reception hardware on site:** check with the real webcam and phones how far and how fast the QR reads (screen brightness, autofocus), that the camera opens on the reception PCs, and that the USB scanner is in keyboard mode with Enter after the code. The Visual C++ 2015-2022 x64 runtime should not be the cause any more: the current OpenCV library does not import it (CI), and the Station setup installs it with the Reception app anyway (CI proves the install step and the self-tests on a runner, not on a clean PC). If the card says "Camera not available", run `Gym.Reception.exe --self-test`: a FAIL on "OpenCV native library" names the DLL or error; all PASS points to the camera itself (driver, in use by another program, Windows camera privacy setting). OpenCvSharpExtern.dll imports Media Foundation (`MF.dll`, `MFPlat.dll`, `MFReadWrite.dll`, CI list): on a Windows "N" edition without the Media Feature Pack it cannot load, so use normal (non-N) Windows on the reception PCs or install that pack.
15. **Admin app gaps (owner decides):** plans have no name of their own (they are shown by their months, "3 months"; names need a database change); members cannot be deactivated or deleted (no API, and no refunds by design); product categories are seeded and not edited in the Admin app (the API can); there is no "gym name on receipts" setting yet.
16. **Theme:** the apps use Deep Teal (dark). The owner chooses between keeping it and a Light theme; a light theme is a swap of `Gym.UI/Themes/Colors.xaml` (see "UI and theme").

## Working rules

- Owner, 2026-11-26: execute all steps without stopping; report at milestones. Steps stay small, each with tests.
- Write tests with new code. Run them and show the real output before calling anything done.
- Commit messages in English. No AI attribution lines.
