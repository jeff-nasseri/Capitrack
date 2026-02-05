/**
 * Tests for CSV import: format detection, parsing (Revolut Stock, Revolut Commodity, Trezor, Generic),
 * deduplication fingerprinting, and the full importCSV pipeline.
 */

const Database = require('better-sqlite3');

// Create the mock database at module level (variable name must start with 'mock')
const mockDb = new Database(':memory:');
mockDb.pragma('journal_mode = WAL');
mockDb.pragma('foreign_keys = ON');

mockDb.exec(`
  CREATE TABLE IF NOT EXISTS accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    type TEXT NOT NULL DEFAULT 'general',
    currency TEXT DEFAULT 'EUR',
    description TEXT DEFAULT '',
    icon TEXT DEFAULT 'wallet',
    color TEXT DEFAULT '#6366f1',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );
  CREATE TABLE IF NOT EXISTS transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER NOT NULL,
    symbol TEXT NOT NULL,
    type TEXT NOT NULL CHECK(type IN ('buy','sell','transfer_in','transfer_out','dividend','interest','fee')),
    quantity REAL NOT NULL DEFAULT 0,
    price REAL NOT NULL DEFAULT 0,
    fee REAL DEFAULT 0,
    currency TEXT DEFAULT 'EUR',
    date TEXT NOT NULL,
    notes TEXT DEFAULT '',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE
  );
  CREATE TABLE IF NOT EXISTS price_cache (
    symbol TEXT PRIMARY KEY,
    price REAL NOT NULL,
    currency TEXT DEFAULT 'USD',
    name TEXT DEFAULT '',
    change_percent REAL DEFAULT 0,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );
`);

mockDb.prepare('INSERT INTO accounts (name, type, currency) VALUES (?, ?, ?)').run('Test Account', 'stock', 'EUR');

jest.mock('../src/db/database', () => mockDb);

afterAll(() => {
  mockDb.close();
});

const { importCSV, detectFormat } = require('../src/services/importer');

describe('Format Detection', () => {
  test('detects Revolut Stock format', () => {
    expect(detectFormat(['Date', 'Ticker', 'Type', 'Quantity', 'Price per share', 'Total Amount', 'Currency', 'FX Rate'])).toBe('revolut-stocks');
  });

  test('detects Revolut Commodity format', () => {
    expect(detectFormat(['Type', 'Product', 'Started Date', 'Completed Date', 'Description', 'Amount', 'Currency', 'State', 'Fee'])).toBe('revolut-commodities');
  });

  test('detects Trezor format', () => {
    expect(detectFormat(['Date', 'Time', 'Transaction ID', 'Type', 'Amount', 'Amount unit', 'Fee', 'Fiat (USD)'])).toBe('trezor');
  });

  test('detects Generic CSV format', () => {
    expect(detectFormat(['symbol', 'type', 'quantity', 'price', 'date'])).toBe('generic');
  });

  test('returns unknown for unrecognized headers', () => {
    expect(detectFormat(['foo', 'bar', 'baz'])).toBe('unknown');
  });

  test('detection is case-insensitive', () => {
    expect(detectFormat(['TICKER', 'PRICE PER SHARE', 'date'])).toBe('revolut-stocks');
  });
});

describe('Revolut Stock Import', () => {
  beforeEach(() => {
    mockDb.prepare('DELETE FROM transactions').run();
  });

  const revolutStockCSV = `Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
2024-01-15,AAPL,BUY - MARKET,10,185.50,$1855.00,USD,1.08
2024-02-10,AAPL,SELL - MARKET,5,192.30,$961.50,USD,1.09
2024-03-01,AAPL,DIVIDEND,0,0,$12.50,USD,1.08
2024-01-20,AAPL,CASH TOP-UP,0,0,$500.00,USD,1.08`;

  test('imports buy/sell/dividend, skips CASH TOP-UP', () => {
    const result = importCSV(revolutStockCSV, 1);
    expect(result.format).toBe('revolut-stocks');
    expect(result.imported).toBe(3);
    expect(result.skipped).toBe(0);
  });

  test('correctly maps transaction types', () => {
    importCSV(revolutStockCSV, 1);
    const txns = mockDb.prepare('SELECT * FROM transactions ORDER BY date').all();
    expect(txns[0].type).toBe('buy');
    expect(txns[0].quantity).toBe(10);
    expect(txns[0].price).toBe(185.5);
    expect(txns[1].type).toBe('sell');
    expect(txns[2].type).toBe('dividend');
  });

  test('deduplicates on second import', () => {
    const first = importCSV(revolutStockCSV, 1);
    expect(first.imported).toBe(3);
    const second = importCSV(revolutStockCSV, 1);
    expect(second.imported).toBe(0);
    expect(second.skipped).toBe(3);
  });
});

describe('Trezor Import', () => {
  beforeEach(() => {
    mockDb.prepare('DELETE FROM transactions').run();
  });

  const trezorCSV = `Date,Time,Transaction ID,Type,Amount,Amount unit,Fee,Fiat (USD)
1/15/2024,12:00,abc123def456,RECV,0.5,BTC,0,$22500
2/10/2024,14:30,xyz789ghi012,SENT,0.1,BTC,0.0001,$4500`;

  test('imports Trezor transactions', () => {
    const result = importCSV(trezorCSV, 1);
    expect(result.format).toBe('trezor');
    expect(result.imported).toBe(2);
  });

  test('maps RECV/SENT to transfer_in/transfer_out', () => {
    importCSV(trezorCSV, 1);
    const txns = mockDb.prepare('SELECT * FROM transactions ORDER BY date').all();
    expect(txns[0].type).toBe('transfer_in');
    expect(txns[0].symbol).toBe('BTC-USD');
    expect(txns[0].quantity).toBe(0.5);
    expect(txns[1].type).toBe('transfer_out');
  });

  test('calculates price from fiat value', () => {
    importCSV(trezorCSV, 1);
    const tx = mockDb.prepare('SELECT * FROM transactions WHERE type = ?').get('transfer_in');
    expect(tx.price).toBeCloseTo(45000, 0); // 22500 / 0.5
  });
});

describe('Revolut Commodity Import', () => {
  beforeEach(() => {
    mockDb.prepare('DELETE FROM transactions').run();
  });

  const commodityCSV = `Type,Product,Started Date,Completed Date,Description,Amount,Currency,State,Fee
EXCHANGE,Gold,2024-01-15 10:00:00,2024-01-15 10:01:00,Exchanged to Gold,0.5,XAU,COMPLETED,0.50
EXCHANGE,Gold,2024-02-20 11:00:00,2024-02-20 11:01:00,Exchanged to EUR,-0.3,XAU,COMPLETED,0.30
EXCHANGE,Gold,2024-03-01 09:00:00,,Pending exchange,0.1,XAU,PENDING,0`;

  test('imports completed commodity trades, skips pending', () => {
    const result = importCSV(commodityCSV, 1);
    expect(result.format).toBe('revolut-commodities');
    expect(result.imported).toBe(2);
  });

  test('maps commodity symbols to Yahoo Finance format', () => {
    importCSV(commodityCSV, 1);
    const txns = mockDb.prepare('SELECT * FROM transactions ORDER BY date').all();
    expect(txns[0].symbol).toBe('GC=F');
    expect(txns[0].type).toBe('buy');
    expect(txns[1].type).toBe('sell');
  });
});

describe('Generic CSV Import', () => {
  beforeEach(() => {
    mockDb.prepare('DELETE FROM transactions').run();
  });

  const genericCSV = `symbol,type,quantity,price,fee,currency,date,notes
AAPL,buy,10,185.50,1.00,USD,2024-01-15,Test buy
MSFT,sell,5,400.00,0.50,USD,2024-02-10,Test sell
INVALID,unknown_type,1,1,0,USD,2024-03-01,Should be skipped`;

  test('imports valid generic CSV rows, skips invalid types', () => {
    const result = importCSV(genericCSV, 1);
    expect(result.format).toBe('generic');
    expect(result.imported).toBe(2);
  });

  test('preserves all fields correctly', () => {
    importCSV(genericCSV, 1);
    const tx = mockDb.prepare('SELECT * FROM transactions WHERE symbol = ?').get('AAPL');
    expect(tx.quantity).toBe(10);
    expect(tx.price).toBe(185.5);
    expect(tx.fee).toBe(1);
    expect(tx.currency).toBe('USD');
    expect(tx.notes).toBe('Test buy');
  });
});

describe('Unknown Format', () => {
  test('returns error for unknown format', () => {
    const csv = 'foo,bar,baz\n1,2,3';
    const result = importCSV(csv, 1);
    expect(result.format).toBe('unknown');
    expect(result.imported).toBe(0);
    expect(result.errors.length).toBeGreaterThan(0);
  });

  test('handles empty CSV', () => {
    const result = importCSV('', 1);
    expect(result.imported).toBe(0);
    expect(result.total).toBe(0);
  });
});

describe('Deduplication', () => {
  beforeEach(() => {
    mockDb.prepare('DELETE FROM transactions').run();
  });

  test('fingerprint is based on account, symbol, type, qty, price, date', () => {
    const csv1 = 'symbol,type,quantity,price,fee,currency,date\nAAPL,buy,10,185.50,0,USD,2024-01-15';
    const csv2 = 'symbol,type,quantity,price,fee,currency,date\nAAPL,buy,10,185.50,5,USD,2024-01-15';

    const r1 = importCSV(csv1, 1);
    expect(r1.imported).toBe(1);

    // Same fingerprint (fee not part of fingerprint) - should skip
    const r2 = importCSV(csv2, 1);
    expect(r2.skipped).toBe(1);
    expect(r2.imported).toBe(0);
  });

  test('different symbols are not deduplicated', () => {
    const csv1 = 'symbol,type,quantity,price,fee,currency,date\nAAPL,buy,10,185.50,0,USD,2024-01-15';
    const csv2 = 'symbol,type,quantity,price,fee,currency,date\nMSFT,buy,10,185.50,0,USD,2024-01-15';

    importCSV(csv1, 1);
    const r2 = importCSV(csv2, 1);
    expect(r2.imported).toBe(1);
  });

  test('within-file deduplication prevents duplicate rows in same file', () => {
    const csv = `symbol,type,quantity,price,fee,currency,date
AAPL,buy,10,185.50,0,USD,2024-01-15
AAPL,buy,10,185.50,0,USD,2024-01-15`;

    const result = importCSV(csv, 1);
    expect(result.imported).toBe(1);
    expect(result.skipped).toBe(1);
  });
});
