# User Guide

This guide walks through every page of Capitrack. Start the app with
`docker compose up -d` and open <http://localhost:3000> (see
[deployment.md](deployment.md)).

The left **sidebar** is the primary navigation: Dashboard, Holdings, Accounts, Activity,
Goals, Calendar, and (at the bottom) a collapse toggle, Settings, and Logout. The sidebar
can be expanded to show labels or collapsed to icons only; the choice is remembered.

## Login


Sign in with your username and password. On a fresh install the database is **empty** and
the default username is **`admin`**. The password is taken from `CAPITRACK_INIT_PASSWORD`;
if you didn't set it, a **random password is generated on first run and printed to the
container logs** (`docker compose logs api`).

> **Change the password** after first login (Settings → Security). To choose the initial
> password yourself, set `CAPITRACK_INIT_PASSWORD` before the first run — see
> [deployment.md](deployment.md).

The session is kept in an `HttpOnly` cookie that lasts 7 days (and slides on use), so you
stay logged in across visits and container restarts.

## Dashboard


The dashboard is the landing page (`/`). It shows:

- **Total wealth** in your main currency, with the absolute gain, gain percent, and the
  label of the selected chart period. A **refresh** button re-fetches live prices.
- A **CET clock** in the corner.
- A **portfolio value chart** with period buttons: **1W, 1M, 3M, 6M, YTD, 1Y, 5Y, ALL**
  (default 3M). Optional transaction dots can be overlaid (toggle in Settings → Appearance).
- **Accounts** list — each row shows the account's market value, cost basis, and gain
  percent. Click a row to open the account.
- **Holdings** — your top holdings by market value (up to five, with a "more" affordance).
  Click a holding to open its symbol page.
- **Saving Goals** — mini progress bars for your top goals (progress = total wealth ÷ target).

The dashboard also saves a background daily-wealth snapshot each time it loads.

## Holdings


The Holdings page (`/holdings`) aggregates positions across **all** accounts into one table:
symbol, quantity, average cost, market value, gain, and gain percent, sorted by market value.
Live prices are fetched in a single batch quote call.

## Accounts


The Accounts page (`/accounts`) shows every account as a card with its icon, name, type
badge, market value, holding count, and currency. Click a card to open the account.

- **Add Account** (top-right) opens a form. Fields: name (required), type, currency, icon,
  color, description, and tags. Currency and type default sensibly if left blank.
- Accounts can also be **edited and deleted** from **Settings → Accounts**.

## Account detail


Opening an account (`/accounts/{id}`) shows:

- Account value in your main currency, with gain/percent for the period and a refresh button.
- Toolbar actions: **Import** (CSV), **Export** (CSV for this account), and **Add
  Transaction**.
- A **value chart** with the same period buttons as the dashboard (scoped to this account).
- Metric cards: **Investments**, **Net Contribution**, and **Cost Basis**.
- A **Holdings** table for the account. Values are converted to your main currency using the
  configured currency rates.

**Importing:** the Import button opens the import modal, which auto-detects the CSV format,
shows a format badge, and reports imported / skipped / total after import. Duplicate rows are
skipped automatically. See [csv-import.md](csv-import.md).

## Symbol detail


Clicking a holding opens the symbol page (`/accounts/{id}/{symbol}`). The header shows the
name, ticker, an **Add Transaction** button, and three tabs.

The **hero** shows the live price and change, plus a holding card with shares, market value,
book value, average cost, % of portfolio, today's return, and total return.

A **price chart** sits above the tabs with the standard period buttons.

Tabs:

- **Overview** — a short "About" section and a price-stats grid (Open, Close, High, Low,
  Prev. Close, Volume). Because the quote API returns only the current price, these reflect
  the latest price where applicable.
- **Lots** — the full transaction history for this symbol in this account, with per-row
  edit/delete.
- **Quotes** — an extended quote grid (Market Cap, P/E Ratio, 52W High/Low, Avg Volume,
  Dividend Yield). These fields are **not** provided by the quote source, so they display as
  **N/A / 0** by design (see [migration.md](migration.md)).

## Activity


The Activity page (`/activity`) is a chronological table of all transactions across accounts:
date, account, symbol, type, quantity, price, and total. A per-row menu offers **View
Details**, **Edit**, and **Delete**.

- **Export** (top-right) downloads all transactions as a CSV file.

## Calendar


The Calendar page (`/calendar`) plots transactions and daily wealth over time, with three
views you can switch between, plus previous/next navigation and a **Today** button:

- **Month** — a 7-column grid (weeks start Monday). Each day shows its daily wealth (when a
  snapshot exists, colored by gain/loss) and up to two transaction chips (with a "+N more"
  indicator).
- **Week** — a column per day showing the day's wealth (with gain percent) and a detailed
  list of that day's transactions.
- **Year** — a tile per month showing the transaction count and total buys/sells for the
  month.

Daily-wealth values come from saved snapshots; opening the calendar triggers a snapshot for
today in the background.

## Goals


The Goals page (`/goals`) lists your financial goals as progress cards. Each card shows the
title, current wealth vs. target amount, a progress bar and percentage, the target date, and
any tags. A goal is marked **achieved** when flagged or when your total wealth reaches the
target. Click a card to edit it; use **Add Goal** to create one.

Goals can also be managed (including **Remove All**) from **Settings → Goals**.

## Settings


Settings (`/settings`) has a left menu with nine panels:

### 1. Accounts
Add, edit, and delete accounts. Shows type, currency, description, and tags per account.

### 2. Goals
Add, edit, and delete goals, with a per-goal progress readout. Includes a **Remove All**
action (double-confirmed).

### 3. Tags
Add, edit, and delete tags (name + color). Tags can be attached to accounts, transactions,
and goals.

### 4. Currency Rates
Manage manual FX rates (`from → to` and a rate). These rates drive the dashboard's
conversion of holdings and cost basis into your main currency. A fresh install seeds
USD↔EUR and GBP/EUR rates.

### 5. Database
Shows the SQLite database path and whether the file exists. You can change the path and save
it; the change is persisted and **takes effect after a restart** (the panel reminds you).
**Refresh Platform** reloads the app against the current database.

### 6. Appearance
- **Main Currency** — the currency all dashboard values are shown in (e.g. EUR, USD, GBP,
  CHF, JPY, CAD, AUD, CNY). Changing it updates your account and reloads totals.
- **Theme Mode** — switch between **Dark** and **Light** (remembered across sessions; applied
  before the app boots to avoid a flash).
- **Show transaction dots on charts** — a toggle that overlays transaction markers on the
  value charts.

### 7. Security
Change your password. Enter the current password, the new password, and a confirmation. The
new password must be at least 8 characters and include an uppercase letter, a lowercase
letter, a number, and a special character (`! @ # $ % ^ & *`).

### 8. About
Shows the app version, license (MIT), author, and links to the GitHub repository and issue
tracker.

### 9. Danger Zone
**Purge All Data** permanently deletes all accounts, transactions, goals, and cached prices.
This is double-confirmed and cannot be undone.

## Logout

The **Logout** button at the bottom of the sidebar ends your session and returns you to the
login page.
