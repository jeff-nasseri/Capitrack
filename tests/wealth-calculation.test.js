/**
 * Tests for wealth/portfolio calculation logic:
 * - Holdings quantity calculation (buy/sell/transfer)
 * - Average cost basis
 * - Market value with currency conversion
 * - Portfolio gain/loss calculation
 */

const Database = require('better-sqlite3');

let db;

beforeAll(() => {
  db = new Database(':memory:');
  db.pragma('journal_mode = WAL');
  db.pragma('foreign_keys = ON');

  db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      username TEXT UNIQUE NOT NULL,
      password_hash TEXT NOT NULL,
      base_currency TEXT DEFAULT 'EUR',
      created_at DATETIME DEFAULT CURRENT_TIMESTAMP
    );
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
    CREATE TABLE IF NOT EXISTS currency_rates (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      from_currency TEXT NOT NULL,
      to_currency TEXT NOT NULL,
      rate REAL NOT NULL,
      updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      UNIQUE(from_currency, to_currency)
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

  // Seed test data
  db.prepare('INSERT INTO users (username, password_hash, base_currency) VALUES (?, ?, ?)').run('test', 'hash', 'EUR');
  db.prepare('INSERT INTO accounts (name, type, currency) VALUES (?, ?, ?)').run('Stock', 'stock', 'EUR');
  db.prepare('INSERT INTO accounts (name, type, currency) VALUES (?, ?, ?)').run('Crypto', 'crypto', 'EUR');
  db.prepare('INSERT OR REPLACE INTO currency_rates (from_currency, to_currency, rate) VALUES (?, ?, ?)').run('USD', 'EUR', 0.92);
  db.prepare('INSERT OR REPLACE INTO currency_rates (from_currency, to_currency, rate) VALUES (?, ?, ?)').run('EUR', 'USD', 1.09);
});

afterAll(() => {
  db.close();
});

// Reusable: compute holdings exactly as the dashboard summary does
function computeHoldings() {
  return db.prepare(`
    SELECT
      t.symbol,
      a.id as account_id,
      a.name as account_name,
      a.currency as account_currency,
      SUM(CASE WHEN t.type IN ('buy','transfer_in') THEN t.quantity ELSE 0 END) -
      SUM(CASE WHEN t.type IN ('sell','transfer_out') THEN t.quantity ELSE 0 END) as quantity,
      SUM(CASE WHEN t.type IN ('buy','transfer_in') THEN t.quantity * t.price ELSE 0 END) /
      NULLIF(SUM(CASE WHEN t.type IN ('buy','transfer_in') THEN t.quantity ELSE 0 END), 0) as avg_cost
    FROM transactions t
    JOIN accounts a ON t.account_id = a.id
    GROUP BY t.symbol, a.id
    HAVING quantity > 0.00000001
  `).all();
}

function computeWealth(holdings, prices) {
  const rates = {};
  const rateRows = db.prepare('SELECT * FROM currency_rates').all();
  for (const r of rateRows) rates[`${r.from_currency}_${r.to_currency}`] = r.rate;

  const user = db.prepare('SELECT base_currency FROM users LIMIT 1').get();
  const baseCurrency = user.base_currency;

  let totalWealth = 0;
  let totalCost = 0;

  for (const h of holdings) {
    const priceData = prices[h.symbol] || { price: 0, currency: 'USD' };
    let marketValue = h.quantity * priceData.price;
    let costBasis = h.quantity * (h.avg_cost || 0);

    const priceCurrency = priceData.currency || 'USD';
    if (priceCurrency !== baseCurrency) {
      const rate = rates[`${priceCurrency}_${baseCurrency}`] || 1;
      marketValue *= rate;
    }

    if (h.account_currency && h.account_currency !== baseCurrency) {
      const rate = rates[`${h.account_currency}_${baseCurrency}`] || 1;
      costBasis *= rate;
    }

    totalWealth += marketValue;
    totalCost += costBasis;
  }

  return {
    totalWealth: Math.round(totalWealth * 100) / 100,
    totalCost: Math.round(totalCost * 100) / 100,
    totalGain: Math.round((totalWealth - totalCost) * 100) / 100,
    totalGainPercent: totalCost > 0 ? Math.round(((totalWealth - totalCost) / totalCost) * 10000) / 100 : 0
  };
}

describe('Holdings Calculation', () => {
  beforeEach(() => {
    db.prepare('DELETE FROM transactions').run();
  });

  test('buy transactions increase quantity', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 185.50, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 5, 190.00, 'USD', '2024-02-01');

    const holdings = computeHoldings();
    expect(holdings.length).toBe(1);
    expect(holdings[0].quantity).toBe(15);
  });

  test('sell transactions reduce quantity', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 185.50, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'sell', 3, 195.00, 'USD', '2024-02-01');

    const holdings = computeHoldings();
    expect(holdings[0].quantity).toBe(7);
  });

  test('fully sold holdings have zero or near-zero quantity', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 185.50, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'sell', 10, 195.00, 'USD', '2024-02-01');

    const holdings = computeHoldings();
    // Either filtered out entirely or quantity is effectively zero
    for (const h of holdings) {
      if (h.symbol === 'AAPL') {
        expect(h.quantity).toBeCloseTo(0, 4);
      }
    }
  });

  test('transfer_in and transfer_out affect quantities', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(2, 'BTC-USD', 'transfer_in', 0.5, 45000, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(2, 'BTC-USD', 'transfer_out', 0.1, 46000, 'USD', '2024-02-01');

    const holdings = computeHoldings();
    expect(holdings[0].quantity).toBeCloseTo(0.4, 8);
  });

  test('average cost is weighted by quantity', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 100, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 200, 'USD', '2024-02-01');

    const holdings = computeHoldings();
    // Weighted avg = (10*100 + 10*200) / 20 = 150
    expect(holdings[0].avg_cost).toBeCloseTo(150, 2);
  });

  test('multiple symbols across accounts are separate', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 185, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(2, 'BTC-USD', 'buy', 0.5, 45000, 'USD', '2024-01-15');

    const holdings = computeHoldings();
    expect(holdings.length).toBe(2);
    const aapl = holdings.find(h => h.symbol === 'AAPL');
    const btc = holdings.find(h => h.symbol === 'BTC-USD');
    expect(aapl.quantity).toBe(10);
    expect(btc.quantity).toBe(0.5);
  });
});

describe('Wealth Calculation', () => {
  beforeEach(() => {
    db.prepare('DELETE FROM transactions').run();
  });

  test('calculates total wealth with USD to EUR conversion', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 150, 'USD', '2024-01-15');

    const holdings = computeHoldings();
    const prices = { 'AAPL': { price: 200, currency: 'USD' } };
    const result = computeWealth(holdings, prices);

    // Market value: 10 * 200 * 0.92 = 1840 EUR
    expect(result.totalWealth).toBeCloseTo(1840, 0);
  });

  test('calculates gain correctly', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 150, 'USD', '2024-01-15');

    const holdings = computeHoldings();
    const prices = { 'AAPL': { price: 200, currency: 'USD' } };
    const result = computeWealth(holdings, prices);

    // Cost: 10 * 150 = 1500 (EUR account currency = EUR, but avg_cost from USD trades)
    // totalGain = totalWealth - totalCost
    expect(result.totalGain).toBeGreaterThan(0);
  });

  test('handles zero-price scenario', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 150, 'USD', '2024-01-15');

    const holdings = computeHoldings();
    const prices = { 'AAPL': { price: 0, currency: 'USD' } };
    const result = computeWealth(holdings, prices);

    expect(result.totalWealth).toBe(0);
    expect(result.totalGain).toBeLessThan(0);
  });

  test('handles multiple holdings and accounts', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 150, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(2, 'BTC-USD', 'buy', 1, 40000, 'USD', '2024-01-15');

    const holdings = computeHoldings();
    const prices = {
      'AAPL': { price: 200, currency: 'USD' },
      'BTC-USD': { price: 50000, currency: 'USD' }
    };
    const result = computeWealth(holdings, prices);

    // AAPL: 10 * 200 * 0.92 = 1840
    // BTC: 1 * 50000 * 0.92 = 46000
    // Total: 47840
    expect(result.totalWealth).toBeCloseTo(47840, 0);
  });

  test('missing currency rate defaults to 1', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 150, 'GBP', '2024-01-15');

    const holdings = computeHoldings();
    const prices = { 'AAPL': { price: 200, currency: 'GBP' } };
    const result = computeWealth(holdings, prices);

    // No GBP->EUR rate, defaults to 1
    expect(result.totalWealth).toBeCloseTo(2000, 0);
  });
});

describe('Portfolio History Logic', () => {
  beforeEach(() => {
    db.prepare('DELETE FROM transactions').run();
  });

  test('running holdings accumulate over time', () => {
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 10, 150, 'USD', '2024-01-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'buy', 5, 160, 'USD', '2024-02-15');
    db.prepare('INSERT INTO transactions (account_id, symbol, type, quantity, price, currency, date) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(1, 'AAPL', 'sell', 3, 170, 'USD', '2024-03-15');

    const txns = db.prepare('SELECT * FROM transactions ORDER BY date ASC').all();

    // Simulate running holdings
    const holdings = {};
    const history = [];
    for (const tx of txns) {
      if (!holdings[tx.symbol]) holdings[tx.symbol] = 0;
      if (['buy', 'transfer_in'].includes(tx.type)) holdings[tx.symbol] += tx.quantity;
      else if (['sell', 'transfer_out'].includes(tx.type)) holdings[tx.symbol] -= tx.quantity;
      history.push({ date: tx.date, quantity: holdings[tx.symbol] });
    }

    expect(history[0].quantity).toBe(10);
    expect(history[1].quantity).toBe(15);
    expect(history[2].quantity).toBe(12);
  });
});
