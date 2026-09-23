# CSV Import

Capitrack can import transactions from CSV files exported by common brokers/wallets, plus a
generic format. Import is available from an account's toolbar (the **Import** button → import
modal) and via the API. Parsing lives in
`src/server/Server.Infrastructure/Services/Import/CsvImportParser.cs` (pure: rows → ledger legs or
rejections); planning and writing in `src/server/Server.Infrastructure/Services/ImporterService.cs`.

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

CSV parsing trims fields, ignores blank lines, strips a UTF-8 BOM, detects `,` or `;` as the
delimiter, and tolerates ragged rows. Numbers are exact decimals: a decimal comma (`0,5`),
thousands separators (`1,284.75`, `1.234,56`), currency signs and codes, and exponents are read
by `DecimalParser`, which detects each file's decimal separator. A value that is not a number
rejects its row with a reason instead of silently becoming 0.

Every data row is accounted for: it becomes one or more transactions ("legs"), or it is
**rejected with a reason** (e.g. an NFT amount, an unsupported type, an uncompleted Revolut exchange).
Nothing is dropped silently.

## How re-imports stay idempotent

Every leg gets an **import key** built only from the source's immutable fields: never fiat
values, labels or the row's position in the file. The key is stored with the transaction, and
the database holds `(account, import key)` unique, so importing the same file twice, or files
that overlap, never duplicates anything:

| Format | Key |
|--------|-----|
| `trezor` | `trezor|{tx id}|{in or out}|{unit}|{address}|{amount}`; network fee: `trezor|{tx id}|fee|{fee unit}` |
| `revolut-stocks` | `revolut-stocks|{date}|{ticker}|{type}|{quantity}|{currency}` |
| `revolut-commodities` | `revolut-commodities|{started date}|{description}|{amount}|{metal}` |
| `generic` | `generic|id|{id}` when the file has an `id` column, else `generic|{symbol}|{type}|{date}|{quantity}|{currency}` |

Genuinely identical rows (two equal receipts on one day) get `#0`, `#1`, … suffixes, so each is
imported once and counted on re-import. A row whose timing changed (a Trezor transfer that was
pending and is now confirmed) is **updated** in place. Transactions imported before keys existed
are **adopted**: matched by symbol, type, quantity and date, and given their key.

An import is one database transaction: all of it is written, or none of it.

## Check & Import (preview)

`POST /api/transactions/import/preview` runs the very same plan as the import and writes
nothing. Per file it reports every row with its status (**new**, **duplicate**, **update**,
**rejected** with the reason), and a reconciliation: rows read = new + duplicates + rejected
(+ rows you left unselected), counts by type, and per asset the amounts received, sent and paid
in fees, the current balance and the balance after the import. `POST
/api/transactions/import/selected` imports the rows you kept (re-reading the files on the
server), so what was previewed is exactly what gets imported.

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
- `quantity` = `Amount` (abs); currency `EUR`. The statement gives no value, so `price` is the
  **market price at the exchange's time** (the hourly candle, else that UTC day's close,
  converted to EUR at the ECB rate) and the `Fee`, stated in metal units, is valued at that
  price. The note says which price was used. A row no provider can price keeps price `0`.
- Date = `Started Date` (falling back to `Completed Date`), read as UTC; rows that are not
  `COMPLETED` or have no date are rejected with a reason.
- Notes are set to `Revolut Commodity: <description> (<metal code>)`.

So `Exchanged to XAU, Amount 1.5` becomes a `buy` of `1.5` `GC=F`, and `Exchanged to EUR,
Amount 0.5` becomes a `sell` of `0.5` `GC=F`.

---

## Format: `trezor`

Trezor Suite transaction export (crypto).

**Recognized headers:** must include `Transaction ID` and `Amount unit`. The parser reads
`Timestamp`, `Date`, `Time`, `Type`, `Transaction ID`, `Fee`, `Fee unit`, `Address`, `Amount`,
`Amount unit` and `Fiat (USD)`.

**Rows → ledger legs.** A Trezor export can list one transaction on several rows (one per
output or token). Each row becomes legs:

| Source `Type` | Legs |
|---------------|------|
| `RECV` | `transfer_in` of `Amount` in `Amount unit` |
| `SENT` | `transfer_out` of `Amount`, plus the network fee |
| `SELF`, `FAILED`, `CONTRACT` | only the network fee (nothing left the wallet except the fee) |
| `JOINT` (coinjoin), other | rejected with a reason |

- **Fees:** a `fee` leg in `Fee unit` (the native coin, even for a token transfer), charged once
  per transaction and fee unit, and only when this wallet sent the transaction. A fee leg
  reduces the holding like a sale at average cost.
- **Account-model chains** (ETH, BNB, POL, SOL, XRP, TRX, …): a transaction debits its native
  coin once; a second native `SENT` row of the same transaction is the contract's internal
  onward transfer and is rejected with that reason. UTXO chains (BTC, LTC, DOGE, BCH, ADA, …)
  count every output.
- **Amounts** that are not numbers (e.g. `ID 0` for an NFT) are rejected with a reason.
- **Time:** the `Timestamp` column (Unix, UTC) is authoritative; `Date` + `Time` (local, with
  their `GMT+n` offset) are the fallback. Transactions keep their exact UTC instant.
- **Price:** `Fiat (USD) / Amount`. Trezor's fiat values are transaction-time values, not
  export-time values (recent rows match CoinGecko's 5-minute price, older ones Trezor's daily
  rate). An empty fiat column is priced from the market like commodities. An explicit `0`
  (an amount worth less than a cent) stays 0.
- Symbol = `Amount unit` as a USD pair (`BTC→BTC-USD`).
- Transfers between your own wallets (the same transaction id and asset on both sides) carry
  their cost basis instead of counting as a sale and a purchase.

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

- **Re-importing is safe.** Import keys make importing the same or overlapping exports add
  nothing the second time (everything is reported as a duplicate).
- **Force a format** by passing `format` to `POST /api/transactions/import/csv` if
  auto-detection picks the wrong one (e.g. a generic file whose headers happen to look like
  another format).
- **Prices for transfers/dividends.** A transfer received from outside Capitrack enters the
  cost basis at its value when received (the fiat value, or the market price when the source has
  none); dividends are stored as a cash amount with price `1`.
- The read-only `./transactions` folder mounted into the API container (see
  [deployment.md](deployment.md)) is a convenient place to stage CSV files for manual import;
  Capitrack does not auto-import from it.
