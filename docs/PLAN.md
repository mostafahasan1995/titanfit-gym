# TitanFit Gym System: Plan

The single source of truth for this project. Any session that continues the work starts here.

## Status

| Step | What | Status |
|---|---|---|
| 1 | Design mockups | ✅ Done (`design/`, logo and name added) |
| 2 | Core: skeleton, CI, database, API, login, subscription logic | 🔄 In progress (2.1–2.4 ✅ done, 2.5 installers next) |
| 3 | Reception app | Waiting (needs Syrian ID scan sample) |
| 4 | Cafeteria app | Waiting |
| 5 | Admin app + deployment | Waiting |

**Next action:** step 2.5 (installers in CI + GitHub Release on tags), then the app screens (steps 3 to 5). The owner said on 2026-11-26 to execute all steps without stopping; report at milestones.

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
        ▲  LAN only, HTTPS + SignalR, no internet needed
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
  Gym.UI              shared TITANFIT theme, styles, controls, fonts, logo
  Gym.Admin           WPF app (admin PC)
  Gym.Reception       WPF app (2 reception PCs)
  Gym.Cafeteria       WPF app (2 cafeteria PCs)
tests/
  Gym.Tests              xUnit: domain rules, API without a database (Windows job)
  Gym.IntegrationTests   xUnit: PostgreSQL + API tests (Linux job, postgres service)
installer/            Inno Setup scripts
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
- **Plans:** 1 / 3 / 6 / 12 months, prices editable by the admin. Sample prices: 150,000 / 400,000 / 750,000 / 1,400,000 SYP. The price paid is copied into the subscription; later price changes do not touch it.
- **Private trainer:** a member of type `PrivateTrainer`. He pays the normal membership, and for training clients the "private training service": price = months × one fixed monthly fee from the settings (default 100,000 SYP, sample; admin edits it). The fee is read at the time of payment and stored with it (decided). Only a private trainer can be linked as a client's trainer; each client keeps his own membership. A private trainer with linked clients cannot be changed to regular until they are unlinked.
- **Staff:** type Admin, Reception, Cashier or Club trainer. The login is optional per staff member (username + password + role Admin/Reception/Cashier); club trainers usually have none. Club trainers get one rating per month (1 to 5 stars + note); setting a month again replaces it; all months are kept.
- **Money:** Syrian pounds (SYP), whole numbers, no decimals. Payment: cash.
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
- **Motion:** short, and respects reduced-motion settings.

## Security

- The QR holds no personal data (random code only).
- Role-based access on every API endpoint.
- Passwords are stored hashed.
- Data stays on the LAN. Daily backups.
- Never commit passwords, connection strings with secrets, or member data. **This repo is public.**

## API overview (step 2.4)

- **Server:** `Gym.Api`, `http://<admin-pc>:5080` (setting `Urls`). Runs as the Windows service "TitanFit API" (`UseWindowsService`) or as a console app for development. Swagger UI (`/swagger`) and `/openapi/v1.json` in Development only.
- **Configuration** (appsettings.json, or environment variables such as `ConnectionStrings__Gym`; nothing secret is committed):
  - `ConnectionStrings:Gym`: PostgreSQL connection string.
  - `Database:MigrateOnStartup` (default true): apply migrations and default data at start.
  - `Storage:DataPath` (default `%ProgramData%\TitanFit`): member photos and the token signing key.
  - `Jwt:Key` (optional, 32+ bytes): otherwise a random key is created once in `{DataPath}/keys`. `Jwt:LifetimeHours` (default 12).
- **Login:** `POST /api/auth/login` returns a JWT (staff id, name, role). Send it as `Authorization: Bearer`. A token stops working at once when the staff member is deactivated or his password, username or role changes.
- **First run:** `GET /api/setup/status` and `POST /api/setup/admin` create the first admin; refused once an admin exists.
- **Errors:** RFC 7807 problem details with a stable `code` (e.g. `insufficient_stock`, `duplicate_national_no`, `not_a_private_trainer`): 400 rule or input, 401 not logged in, 403 role, 404 not found, 409 conflict.
- **Live updates:** SignalR hub `/hubs/gym` (any logged-in staff) pushes `CheckInRecorded`, `MemberChanged`, `StockChanged`, `ProductChanged`, `SaleCompleted`, `SettingsChanged`. `Gym.Client.GymLiveConnection` wraps it (auto-reconnect).
- **Client:** `Gym.Client.GymApiClient` has one method per endpoint below.

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
| `GET /api/settings` | A R C | expiring-soon threshold, private training fee |
| `PUT /api/settings` | A | change the settings |
| `/hubs/gym` (SignalR) | A R C | live updates |

## Build and release (GitHub Actions)

The workflow file is `.github/workflows/build.yml`. It is added in step 2.1.

- **Triggers:** push and pull request to `master`, and tags `v*`.
- **Job on `windows-latest`:**
  1. Set up .NET 10, then restore, build (Release) and test.
  2. Publish `Gym.Api`, `Gym.Admin`, `Gym.Reception`, `Gym.Cafeteria` as standalone exes: `-r win-x64 --self-contained -p:PublishSingleFile=true` (WPF apps also `-p:IncludeNativeLibrariesForSelfExtract=true`). The gym PCs need no .NET install.
  3. Smoke test: each WPF exe runs with `--smoke-screenshot <path.png> --lang en|ar`. It renders the main window offscreen at 1440×900, checks that the embedded fonts loaded, saves a PNG and exits (non-zero on error).
  4. Build the installers with Inno Setup (step 2.5). It is not certain that the runner has it preinstalled: check, and install it with `choco install innosetup` if missing.
- **Installers:**
  - `TitanFit-Server-Setup.exe` (admin PC):
    - Installs Gym.Api as a Windows service, plus the Admin app.
    - Adds a firewall rule for the API port.
    - PostgreSQL is installed once with its official installer (not bundled).
  - `TitanFit-Station-Setup.exe`: choose Reception or Cafeteria, and enter the server address.
- **Outputs:**
  - Every run uploads the artifacts `titanfit-exes` (the 4 exes), `screenshots` (EN + AR PNG per app), `test-results` and `integration-test-results` (trx). The setups join in step 2.5.
  - A `v*` tag also attaches them to a GitHub Release.
- **Database tests:** GitHub service containers work only on Linux runners. The `integration` job (`ubuntu-latest`, `postgres:17` service) runs `tests/Gym.IntegrationTests` (database + API tests, each test class on its own new database) and uploads `integration-test-results`. The Windows job runs only `tests/Gym.Tests`.
- **Migrations:** no .NET SDK is needed locally. Run the `ef-migration` workflow (Actions tab, input: migration name), download the `ef-migration` artifact and commit the files in `src/Gym.Infrastructure/Persistence/Migrations`. The integration tests fail if the model and the migrations differ.
- **Fetching the files back:**
  ```
  gh run download <run-id> -R mostafahasan1995/titanfit-gym
  gh release download v0.1.0 -R mostafahasan1995/titanfit-gym
  ```
- **Unsigned exes:** Windows SmartScreen shows a warning (More info → Run anyway). A code-signing certificate can be added later.

## Steps

1. **Design mockups.** ✅ Done.
2. **Core**, in small parts, each with tests and real test output:
   1. Solution skeleton (all projects, empty TITANFIT windows) + build workflow → first exes from Actions. ✅ Done: `TitanFit.slnx`, `GET /health` + 3 tests, `Gym.UI` theme (Deep Teal, fonts, logo, shared app shell with EN / عربي), 3 WPF shells, CI with exes and EN/AR screenshots.
   2. Domain: subscription dates and `GetStatus` + unit tests. ✅ Done.
   3. Database: EF Core model + first migration + PostgreSQL integration tests. ✅ Done.
   4. API: endpoints, SignalR hub, staff login with roles + tests. ✅ Done (see "API overview"). CI: 165 unit tests (Windows job) and 49 PostgreSQL/API integration tests (Linux job).
   5. Installers (Inno Setup) in CI + GitHub Release on tags.
3. **Reception app:**
   - Theme in `Gym.UI`.
   - Camera photo, ID scan, new member, QR create (copy/save).
   - Renew and search, QR check-in with the camera.
4. **Cafeteria app:** cashier screen, stock, low-stock warnings, receipts, shift close.
5. **Admin app and deployment:**
   - Dashboard, management screens, reports, backups.
   - Final installers, and an on-site hardware test.

## Data model (as built in step 2.3)

PostgreSQL, snake_case names, money as whole SYP (`bigint`), timestamps in UTC, days as `date` (the server's local time zone decides "today").

- `members`: id (uuid), member_no (unique, from the sequence `member_numbers`; shown as #00412), type (Regular / PrivateTrainer), first name, father's name, surname, mother's name, birth date, birth place, national_no (unique when present), phone (stored without spaces), notes, photo_file, qr_code (unique, random `TF1-` + 24 characters), private_trainer_id (→ members, nullable), search_name (normalized names), created/updated at.
- `plans`: months, price, active.
- `subscriptions`: member, kind (Membership / PrivateTraining), plan (null for the private service), months, start_date, end_date (inclusive), price_paid, created_by (→ staff), created_at.
- `check_ins`: member (null when the code matched nobody), at, station, method (QrCode / MemberNo), scanned_value, result (Active / ExpiringSoon / Expired / NoSubscription / UnknownMember), days left / overdue, staff.
- `staff`: full name, phone, type (Admin / Reception / Cashier / ClubTrainer), active, username (unique, optional), password_hash (ASP.NET Identity PBKDF2), role (optional), security_stamp, created_at, last_login_at.
- `staff_ratings`: staff, year, month (unique per staff and month), stars (1-5), note, rated_by, created/updated at.
- `product_categories`: name EN/AR, sort order, active (seeded: Drinks, Shakes, Snacks, Supplements).
- `products`: name EN/AR, category, barcode (unique when present), price, min_stock, active.
- `stock_movements`: product, quantity (+/−), type (StockIn / Sale / Adjustment), sale, note, staff, at. Current stock = sum.
- `sales` + `sale_lines`: number (receipt, from the sequence `sale_numbers`), station, cashier, shift, at, total, cash_received; product, quantity, unit price, line total.
- `shifts`: cashier, station, opened_at, closed_at, opening_cash, closing_cash, note. One open shift per cashier (partial unique index).
- `settings`: one row: expiring_soon_days (3), private_training_monthly_fee (100,000).
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

1. **Syrian ID barcode:** what it actually holds is unknown. Before step 3, the owner scans one ID into Notepad and sends the text (digits may be masked).
2. **Receipt printer model** (before step 4).
3. **Touchscreens:** not bought yet. The UI already uses touch-size targets.
4. **Webcams:** buy autofocus models; fixed-focus ones struggle with a QR on a phone screen. Decide one or two per reception PC; a second keeps check-in running during registration.
5. ~~**Early renewal**~~ decided: the new period starts the day after the old end date (see "Business rules").
6. **Expiring threshold:** 3 days, a setting the admin can change (`PUT /api/settings`).
7. **Arabic digits:** 0-9 now. It can be switched to ٠-٩.
8. **Backup target:** which external drive.
9. **Repo is public:** the owner is considering making it private.
10. **Fingerprint reader model:** confirm before coding (e.g. ZKTeco ZK9500).
11. **Negative stock:** refused for now (see "Business rules"). The owner can ask to allow selling with a warning instead.
12. **HTTPS on the LAN:** the API listens on `http://0.0.0.0:5080` for now; the certificate and HTTPS setup come with the server installer (step 2.5).

## Working rules

- Owner, 2026-11-26: execute all steps without stopping; report at milestones. Steps stay small, each with tests.
- Write tests with new code. Run them and show the real output before calling anything done.
- Commit messages in English. No AI attribution lines.
