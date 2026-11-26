# TitanFit Gym System: Plan

The single source of truth for this project. Any session that continues the work starts here.

## Status

| Step | What | Status |
|---|---|---|
| 1 | Design mockups | ✅ Done (`design/`, logo and name added) |
| 2 | Core: skeleton, CI, database, API, login, subscription logic | ⏭ Next |
| 3 | Reception app | Waiting (needs Syrian ID scan sample) |
| 4 | Cafeteria app | Waiting |
| 5 | Admin app + deployment | Waiting |

**Next action:** step 2.1 (solution skeleton + build workflow), then wait for the owner's OK before each next part.

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
  Gym.Domain          entities + rules (subscription status, stock)
  Gym.Infrastructure  EF Core, PostgreSQL, migrations
  Gym.Api             REST + SignalR + role-based auth
  Gym.Client          shared typed API client for the apps
  Gym.UI              shared TITANFIT theme, styles, controls, fonts, logo
  Gym.Admin           WPF app (admin PC)
  Gym.Reception       WPF app (2 reception PCs)
  Gym.Cafeteria       WPF app (2 cafeteria PCs)
tests/
  Gym.Tests           xUnit
installer/            Inno Setup scripts
design/               UI mockups (step 1)
docs/PLAN.md          this file
.github/workflows/    CI build
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
- **Plans:** 1 / 3 / 6 / 12 months, prices editable by the admin. Sample prices: 150,000 / 400,000 / 750,000 / 1,400,000 SYP.
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
- **Roles:** Admin, Reception, Cashier. Staff log in with a username and password.

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

## Build and release (GitHub Actions)

The workflow file is `.github/workflows/build.yml`. It is added in step 2.1.

- **Triggers:** push and pull request to `master`, and tags `v*`.
- **Job on `windows-latest`:**
  1. Set up .NET 10, then restore, build (Release) and test.
  2. Publish `Gym.Api`, `Gym.Admin`, `Gym.Reception`, `Gym.Cafeteria` as standalone exes: `-r win-x64 --self-contained -p:PublishSingleFile=true`. The gym PCs need no .NET install.
  3. Build the installers with Inno Setup. It is not certain that the runner has it preinstalled: check, and install it with `choco install innosetup` if missing.
- **Installers:**
  - `TitanFit-Server-Setup.exe` (admin PC):
    - Installs Gym.Api as a Windows service, plus the Admin app.
    - Adds a firewall rule for the API port.
    - PostgreSQL is installed once with its official installer (not bundled).
  - `TitanFit-Station-Setup.exe`: choose Reception or Cafeteria, and enter the server address.
- **Outputs:**
  - Every run uploads the exes and setups as a workflow artifact.
  - A `v*` tag also attaches them to a GitHub Release.
- **Database tests:** GitHub service containers work only on Linux runners. Run PostgreSQL integration tests in a separate `ubuntu-latest` job with a `postgres` service container.
- **Fetching the files back:**
  ```
  gh run download <run-id> -R mostafahasan1995/titanfit-gym
  gh release download v0.1.0 -R mostafahasan1995/titanfit-gym
  ```
- **Unsigned exes:** Windows SmartScreen shows a warning (More info → Run anyway). A code-signing certificate can be added later.

## Steps

1. **Design mockups.** ✅ Done.
2. **Core**, in small parts, each with tests and real test output:
   1. Solution skeleton (all projects, empty TITANFIT windows) + build workflow → first exes from Actions.
   2. Domain: subscription dates and `GetStatus` + unit tests.
   3. Database: EF Core model + first migration + PostgreSQL integration tests.
   4. API: endpoints, SignalR hub, staff login with roles + tests.
   5. Installers (Inno Setup) in CI + GitHub Release on tags.
3. **Reception app:**
   - Theme in `Gym.UI`.
   - Camera photo, ID scan, new member, QR create (copy/save).
   - Renew and search, QR check-in with the camera.
4. **Cafeteria app:** cashier screen, stock, low-stock warnings, receipts, shift close.
5. **Admin app and deployment:**
   - Dashboard, management screens, reports, backups.
   - Final installers, and an on-site hardware test.

## Draft data model (confirm in step 2.3)

- `User` (username, password hash, role, active)
- `Member` (member no, first name, father's name, surname, mother's name, birth date, birth place, national no (unique), phone, photo, QR code (unique), created at)
- `Plan` (months, price, active)
- `Subscription` (member, months, start date, end date, price paid, created by, created at)
- `CheckIn` (member, time, station, result)
- `Product` (name EN/AR, category, barcode, price, min stock)
- `StockMovement` (product, quantity delta, type, sale, time, user)
- `Sale` + `SaleLine` (number, station, cashier, time, total; product, qty, unit price)
- `Shift` (cashier, station, opened at, closed at)

## Incoming requirements (from the owner, not designed yet)

Answers received during step 2.1. Not built yet. The data model in step 2.3 must leave room for them.

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
5. **Early renewal:** does the new period start from the old end date or from today? Suggested: from the old end date, so no days are lost.
6. **Expiring threshold:** 3 days for now, changeable.
7. **Arabic digits:** 0-9 now. It can be switched to ٠-٩.
8. **Backup target:** which external drive.
9. **Repo is public:** the owner is considering making it private.
10. **Fingerprint reader model:** confirm before coding (e.g. ZKTeco ZK9500).

## Working rules

- Small steps. Wait for the owner's OK before each next part.
- Write tests with new code. Run them and show the real output before calling anything done.
- Commit messages in English. No AI attribution lines.
