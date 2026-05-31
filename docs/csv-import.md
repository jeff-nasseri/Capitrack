# CSV Import

Capitrack can import transactions from CSV files exported by common brokers/wallets, plus a
generic format. Import is available from an account's toolbar (the **Import** button → import
modal) and via the API. The implementation lives in
`src/Capitrack.Api/Services/ImporterService.cs`.

Four formats are supported:

- `revolut-stocks`
- `revolut-commodities`
- `trezor`
- `generic`

## How auto-detection works

When you select a file, the app first calls `POST /api/transactions/import/detect`, which
parses the header row and chooses a format by inspecting (case-insensitive) the column names:

| Detected format | Recognized when headers include |
|-----------------|---------------------------------|
| `revolut-stocks` | `ticker` **and** `price per share` |
| `revolut-commodities` | `product` **and** `started date` **and** `state` |
| `trezor` | `transaction id` **and** `amount unit` |
| `generic` | `symbol` **and** `type` |
| `unknown` | none of the above |

The modal shows the detected format as a badge. On import (`POST
/api/transactions/import/csv`) the same detection runs unless you pass an explicit `format`.
If the format is `unknown`, nothing is imported and the result lists the headers it saw.

CSV parsing trims fields, ignores blank lines, and tolerates ragged rows (missing/extra
fields don't abort the import).

## How de-duplication works

Every parsed row is reduced to a **fingerprint** and compared against the fingerprints of
transactions **already in the target account** (and against rows seen earlier in the same
file). Matching rows are **skipped**, not inserted.

The fingerprint is:

```
{account_id}|{symbol}|{type}|{quantity:F8}|{price:F4}|{date}
```

where `date` is the date part only (anything after a `T` or space is dropped). This means
re-importing the same export is safe and idempotent: quantities are compared to 8 decimals,
prices to 4, and only the calendar date matters.

The import result reports counts:

```json
{ "imported": 8, "skipped": 2, "total": 10, "errors": [], "format": "revolut-stocks" }
```

`total` is the number of parsed (mappable) rows; rows that a parser skips entirely (wrong
state, unsupported type, missing date, etc.) never reach this count.

## Transaction types and symbol mapping

Imported rows map onto Capitrack's transaction types: `buy`, `sell`, `transfer_in`,
`transfer_out`, `dividend` (and, for the generic format, also `interest` / `fee`). Some
formats remap source tickers to Yahoo Finance symbols:

- **Commodities (Revolut):** `XAU → GC=F`, `XAG → SI=F`, `XPT → PL=F`, `XPD → PA=F`.
- **Crypto (Trezor):** `BTC → BTC-USD`, `ETH → ETH-USD`, `LTC → LTC-USD`; any other unit
  `XYZ → XYZ-USD`.

A recurring rule across formats: a **dividend** (or a cash-style commodity exchange) is
recorded as an **amount**, i.e. the cash value goes into `quantity` and `price` is set so the
total reflects the amount (price `1` for dividends, `0` for commodity exchanges).

---

## Format: `revolut-stocks`

Revolut stock account statement.

**Recognized headers:** must include `Ticker` and `Price per share`. The parser also reads
`Type`, `Quantity`, `Total Amount`, `Currency`, and `Date`.

**Example:**

```csv
Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency
2024-01-15T10:30:00.000Z,AAPL,BUY - MARKET,10,$150.00,$1500.00,USD
2024-02-20T14:00:00.000Z,AAPL,SELL - MARKET,5,$160.00,$800.00,USD
2024-03-01T09:00:00.000Z,AAPL,DIVIDEND,,,$12.50,USD
2024-03-10T09:00:00.000Z,TSLA,STOCK SPLIT,3,,,USD
2024-03-11T09:00:00.000Z,,CASH TOP-UP,,,$500.00,USD
```

**Row → transaction mapping:**

| Source `Type` | Mapped type | Quantity | Price | Notes |
|---------------|-------------|----------|-------|-------|
| `BUY - MARKET` | `buy` | `Quantity` (abs) | `Price per share` | |
| `SELL - MARKET` | `sell` | `Quantity` (abs) | `Price per share` | |
| `DIVIDEND` | `dividend` | **`Total Amount`** (abs) | `1` | dividend recorded as cash amount |
| `STOCK SPLIT` | `transfer_in` | `Quantity` | `Price per share` (or `0` when qty & total are 0) | |
| `CASH TOP-UP`, `CASH WITHDRAWAL`, blank ticker | — | — | — | skipped |

Details:
- Monetary fields are cleaned of currency symbols/thousands separators before parsing
  (only digits, `.`, and `-` are kept).
- `Currency` defaults to `USD`; the ticker is upper-cased.
- `Date` is normalized to `YYYY-MM-DD`; rows without a parseable date are skipped.
- Notes are set to `Revolut: <original type>`.

---

## Format: `revolut-commodities`

Revolut commodities (precious-metals) statement, where holdings are exchanged to/from fiat.

**Recognized headers:** must include `Product`, `Started Date`, and `State`. The parser also
reads `Description`, `Amount`, `Fee`, `Currency`, and (as a fallback date) `Completed Date`.

**Example:**

```csv
Product,Started Date,Completed Date,Description,Amount,Fee,Currency,State
Commodities,2024-01-10 09:15:00,2024-01-10 09:15:05,Exchanged to XAU,1.5,0.01,XAU,COMPLETED
Commodities,2024-02-15 11:00:00,2024-02-15 11:00:04,Exchanged to EUR,0.5,0.00,XAU,COMPLETED
Commodities,2024-02-20 08:00:00,,Exchanged to XAG,10,0.02,XAG,PENDING
```

**Row → transaction mapping:**

- Only rows with `State == COMPLETED` are processed.
- The symbol is the metal code mapped to a Yahoo futures symbol (`XAU→GC=F`, `XAG→SI=F`,
  `XPT→PL=F`, `XPD→PA=F`; the `Currency` column carries the metal code).
- Direction is inferred from `Description`:
  - contains `Exchanged to EUR` or `Exchanged to USD` → **`sell`** (metal sold for fiat)
  - otherwise starts with `Exchanged to` → **`buy`** (fiat exchanged into metal)
  - anything else → skipped
- `quantity` = `Amount` (abs); `price` = `0` (this is an amount-style record); `fee` = `Fee`
  (abs); currency is recorded as `EUR`.
- Date = the date part of `Started Date` (falling back to `Completed Date`); rows without a
  date are skipped.
- Notes are set to `Revolut Commodity: <description> (<metal code>)`.

So `Exchanged to XAU, Amount 1.5` becomes a `buy` of `1.5` `GC=F`, and `Exchanged to EUR,
Amount 0.5` becomes a `sell` of `0.5` `GC=F`.

---

## Format: `trezor`

Trezor Suite transaction export (crypto).

**Recognized headers:** must include `Transaction ID` and `Amount unit`. The parser also
reads `Type`, `Amount`, `Fiat (USD)`, `Fee`, and `Date`.

**Example:**

```csv
Transaction ID,Date,Type,Amount,Amount unit,Fiat (USD),Fee
a1b2c3d4e5f6a7b8c9,1/15/2024,RECV,0.05,BTC,2150.00,0.00010
f6e5d4c3b2a1f0e9d8,3/02/2024,SENT,0.02,BTC,1300.00,0.00008
00aa11bb22cc33dd44,2/10/2024,RECV,1.5,ETH,4200.00,0.0021
```

**Row → transaction mapping:**

| Source `Type` | Mapped type |
|---------------|-------------|
| `RECV` | `transfer_in` |
| `SENT` | `transfer_out` |
| other | skipped |

Details:
- Symbol = `Amount unit` mapped to a Yahoo crypto symbol (`BTC→BTC-USD`, `ETH→ETH-USD`,
  `LTC→LTC-USD`); any other unit becomes `<unit>-USD`.
- `quantity` = `Amount` (abs); `price` = `Fiat (USD) / Amount` (a derived per-unit USD
  price), so quantity × price ≈ the USD value of the transfer; `fee` = `Fee` (abs); currency
  is `USD`.
- `Date` is `M/D/YYYY` and is normalized to `YYYY-MM-DD`. Rows with no date or a zero amount
  are skipped.
- Notes are `TxID: <first 16 chars>...` (or `Trezor <unit>` when no transaction id).

---

## Format: `generic`

A simple, broker-agnostic format — the easiest to produce by hand and the one Capitrack's own
**Export CSV** is closest to.

**Recognized headers:** must include `symbol` and `type`. Recognized columns (each accepted
in lower-, Title-, or UPPER-case) are: `symbol`, `type`, `quantity`, `price`, `fee`,
`currency`, `date`, `notes`.

**Example:**

```csv
symbol,type,quantity,price,fee,currency,date,notes
AAPL,buy,10,150,1.00,USD,2024-01-15,Initial position
AAPL,sell,5,160,1.00,USD,2024-02-20,Trim
BTC-USD,transfer_in,0.05,42000,0,USD,2024-01-10,From cold wallet
VWRL,dividend,0,0,0,EUR,2024-03-01,Q1 dividend
```

**Row → transaction mapping:**

- `type` is lower-cased and must be one of `buy`, `sell`, `transfer_in`, `transfer_out`,
  `dividend`, `interest`, `fee`; rows with any other type are skipped.
- `symbol` is upper-cased and is **required**; `date` is **required** (used as-is).
- `quantity`, `price`, `fee` default to `0`; `currency` defaults to `EUR`; `notes` is
  optional.
- No symbol remapping is performed — values are imported exactly as given.

---

## Tips

- **Re-importing is safe.** Because of fingerprint de-dup, importing the same export twice
  adds nothing the second time (everything is counted as skipped).
- **Force a format** by passing `format` to `POST /api/transactions/import/csv` if
  auto-detection picks the wrong one (e.g. a generic file whose headers happen to look like
  another format).
- **Prices for transfers/dividends.** Transfers (Trezor) get a derived price from the fiat
  value; dividends are stored as a cash amount with price `1`. Keep this in mind when reading
  the resulting cost-basis numbers.
- The read-only `./transactions` folder mounted into the API container (see
  [deployment.md](deployment.md)) is a convenient place to stage CSV files for manual import;
  Capitrack does not auto-import from it.
