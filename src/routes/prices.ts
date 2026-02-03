import express, { Response } from 'express';
import db from '../db/database';
import { AuthenticatedRequest, PriceCache, CurrencyRate, User } from '../types';

const router = express.Router();

let yahooFinance: any = null;

async function getYahooFinance(): Promise<any> {
  if (!yahooFinance) {
    const yf = await import('yahoo-finance2');
    yahooFinance = new yf.default();
  }
  return yahooFinance;
}

function daysAgo(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().split('T')[0];
}

// Get cached price for a symbol
router.get('/quote/:symbol', async (req: AuthenticatedRequest, res: Response): Promise<void> => {
  const symbol = req.params.symbol.toUpperCase();

  // Check cache (5 minutes)
  const cached = db.prepare('SELECT * FROM price_cache WHERE symbol = ? AND updated_at > datetime(\'now\', \'-5 minutes\')').get(symbol) as PriceCache | undefined;
  if (cached) {
    res.json(cached);
    return;
  }

  try {
    const yf = await getYahooFinance();
    const quote = await yf.quote(symbol);

    const data = {
      symbol: symbol,
      price: quote.regularMarketPrice || 0,
      currency: quote.currency || 'USD',
      name: quote.shortName || quote.longName || symbol,
      change_percent: quote.regularMarketChangePercent || 0
    };

    db.prepare(`
      INSERT OR REPLACE INTO price_cache (symbol, price, currency, name, change_percent, updated_at)
      VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
    `).run(data.symbol, data.price, data.currency, data.name, data.change_percent);

    res.json(data);
  } catch (e) {
    // Return cached even if stale
    const stale = db.prepare('SELECT * FROM price_cache WHERE symbol = ?').get(symbol) as PriceCache | undefined;
    if (stale) {
      res.json({ ...stale, stale: true });
      return;
    }
    res.status(404).json({ error: `Could not fetch price for ${symbol}: ${(e as Error).message}` });
  }
});

// Batch quote for multiple symbols
router.post('/quotes', async (req: AuthenticatedRequest, res: Response): Promise<void> => {
  const { symbols } = req.body;
  if (!symbols || !Array.isArray(symbols)) {
    res.status(400).json({ error: 'symbols array required' });
    return;
  }

  const results: Record<string, any> = {};
  const toFetch: string[] = [];

  // Check cache first
  for (const sym of symbols) {
    const s = sym.toUpperCase();
    const cached = db.prepare('SELECT * FROM price_cache WHERE symbol = ? AND updated_at > datetime(\'now\', \'-5 minutes\')').get(s) as PriceCache | undefined;
    if (cached) {
      results[s] = cached;
    } else {
      toFetch.push(s);
    }
  }

  // Fetch remaining
  if (toFetch.length > 0) {
    try {
      const yf = await getYahooFinance();
      for (const symbol of toFetch) {
        try {
          const quote = await yf.quote(symbol);
          const data = {
            symbol,
            price: quote.regularMarketPrice || 0,
            currency: quote.currency || 'USD',
            name: quote.shortName || quote.longName || symbol,
            change_percent: quote.regularMarketChangePercent || 0
          };

          db.prepare(`
            INSERT OR REPLACE INTO price_cache (symbol, price, currency, name, change_percent, updated_at)
            VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
          `).run(data.symbol, data.price, data.currency, data.name, data.change_percent);

          results[symbol] = data;
        } catch (e) {
          const stale = db.prepare('SELECT * FROM price_cache WHERE symbol = ?').get(symbol) as PriceCache | undefined;
          if (stale) results[symbol] = { ...stale, stale: true };
          else results[symbol] = { symbol, price: 0, error: (e as Error).message };
        }
      }
    } catch {
      // If yahoo-finance fails globally, return stale cache for remaining
      for (const symbol of toFetch) {
        if (!results[symbol]) {
          const stale = db.prepare('SELECT * FROM price_cache WHERE symbol = ?').get(symbol) as PriceCache | undefined;
          if (stale) results[symbol] = { ...stale, stale: true };
          else results[symbol] = { symbol, price: 0, error: 'Service unavailable' };
        }
      }
    }
  }

  res.json(results);
});

// Get historical data for charting
router.get('/history/:symbol', async (req: AuthenticatedRequest, res: Response): Promise<void> => {
  const symbol = req.params.symbol.toUpperCase();
  const period = (req.query.period as string) || '1y';

  const periodMap: Record<string, { period1: string }> = {
    '1w': { period1: daysAgo(7) },
    '1m': { period1: daysAgo(30) },
    '3m': { period1: daysAgo(90) },
    '6m': { period1: daysAgo(180) },
    '1y': { period1: daysAgo(365) },
    '5y': { period1: daysAgo(1825) },
    'max': { period1: '2000-01-01' }
  };

  const range = periodMap[period] || periodMap['1y'];

  try {
    const yf = await getYahooFinance();
    const result = await yf.chart(symbol, {
      period1: range.period1,
      interval: period === '1w' ? '1h' : period === '1m' ? '1d' : '1wk'
    });

    const data = (result.quotes || []).map((q: any) => ({
      date: q.date,
      open: q.open,
      high: q.high,
      low: q.low,
      close: q.close,
      volume: q.volume
    })).filter((q: any) => q.close !== null);

    res.json(data);
  } catch (e) {
    res.status(404).json({ error: `Could not fetch history for ${symbol}: ${(e as Error).message}` });
  }
});

// Search symbols
router.get('/search/:query', async (req: AuthenticatedRequest, res: Response): Promise<void> => {
  try {
    const yf = await getYahooFinance();
    const result = await yf.search(req.params.query);
    const quotes = (result.quotes || []).map((q: any) => ({
      symbol: q.symbol,
      name: q.shortname || q.longname || q.symbol,
      type: q.quoteType,
      exchange: q.exchDisp || q.exchange
    }));
    res.json(quotes);
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
});

// Dashboard summary - total wealth calculation
router.get('/dashboard/summary', async (_req: AuthenticatedRequest, res: Response): Promise<void> => {
  try {
    // Get all holdings across all accounts
    const holdings = db.prepare(`
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
    `).all() as Array<{ symbol: string; account_id: number; account_name: string; account_currency: string; quantity: number; avg_cost: number }>;

    // Gather all unique symbols
    const symbols = [...new Set(holdings.map(h => h.symbol))];

    // Fetch prices
    const prices: Record<string, PriceCache> = {};
    if (symbols.length > 0) {
      const yf = await getYahooFinance();
      for (const symbol of symbols) {
        try {
          const cached = db.prepare('SELECT * FROM price_cache WHERE symbol = ? AND updated_at > datetime(\'now\', \'-5 minutes\')').get(symbol) as PriceCache | undefined;
          if (cached) {
            prices[symbol] = cached;
          } else {
            const quote = await yf.quote(symbol);
            prices[symbol] = {
              symbol,
              price: quote.regularMarketPrice || 0,
              currency: quote.currency || 'USD',
              name: quote.shortName || quote.longName || symbol,
              change_percent: quote.regularMarketChangePercent || 0,
              updated_at: new Date().toISOString()
            };
            db.prepare(`
              INSERT OR REPLACE INTO price_cache (symbol, price, currency, name, change_percent, updated_at)
              VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
            `).run(symbol, prices[symbol].price, prices[symbol].currency, prices[symbol].name, prices[symbol].change_percent);
          }
        } catch {
          const stale = db.prepare('SELECT * FROM price_cache WHERE symbol = ?').get(symbol) as PriceCache | undefined;
          prices[symbol] = stale || { symbol, price: 0, currency: 'USD', name: symbol, change_percent: 0, updated_at: '' };
        }
      }
    }

    // Get currency rates
    const rates: Record<string, number> = {};
    const rateRows = db.prepare('SELECT * FROM currency_rates').all() as CurrencyRate[];
    for (const r of rateRows) {
      rates[`${r.from_currency}_${r.to_currency}`] = r.rate;
    }

    // Get user base currency
    const user = db.prepare('SELECT base_currency FROM users LIMIT 1').get() as User | undefined;
    const baseCurrency = user ? user.base_currency : 'EUR';

    // Calculate totals per account
    const accountSummaries: Record<number, { account_id: number; account_name: string; market_value: number; cost_basis: number; holdings_count: number }> = {};
    let totalWealth = 0;
    let totalCost = 0;

    for (const h of holdings) {
      const priceData = prices[h.symbol] || { price: 0, currency: 'USD' };
      let marketValue = h.quantity * priceData.price;
      let costBasis = h.quantity * (h.avg_cost || 0);

      // Convert to base currency if needed
      const priceCurrency = priceData.currency || 'USD';
      if (priceCurrency !== baseCurrency) {
        const rateKey = `${priceCurrency}_${baseCurrency}`;
        const rate = rates[rateKey] || 1;
        marketValue *= rate;
      }

      if (h.account_currency && h.account_currency !== baseCurrency) {
        const rateKey = `${h.account_currency}_${baseCurrency}`;
        const rate = rates[rateKey] || 1;
        costBasis *= rate;
      }

      if (!accountSummaries[h.account_id]) {
        accountSummaries[h.account_id] = {
          account_id: h.account_id,
          account_name: h.account_name,
          market_value: 0,
          cost_basis: 0,
          holdings_count: 0
        };
      }

      accountSummaries[h.account_id].market_value += marketValue;
      accountSummaries[h.account_id].cost_basis += costBasis;
      accountSummaries[h.account_id].holdings_count++;
      totalWealth += marketValue;
      totalCost += costBasis;
    }

    res.json({
      total_wealth: totalWealth,
      total_cost: totalCost,
      total_gain: totalWealth - totalCost,
      total_gain_percent: totalCost > 0 ? ((totalWealth - totalCost) / totalCost) * 100 : 0,
      base_currency: baseCurrency,
      accounts: Object.values(accountSummaries),
      holdings_count: holdings.length
    });
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
});

// Portfolio value history (calculated from transactions + price history)
router.get('/portfolio/history', async (req: AuthenticatedRequest, res: Response): Promise<void> => {
  try {
    const { account_id, period } = req.query;
    const periodDays: Record<string, number | null> = { '1w': 7, '1m': 30, '3m': 90, '6m': 180, 'ytd': null, '1y': 365, '5y': 1825, 'all': 3650 };
    const days = periodDays[(period as string) || '3m'] ?? 90;

    let startDate: string;
    if (period === 'ytd') {
      startDate = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];
    } else {
      startDate = daysAgo(days);
    }

    // Get holdings at each point in time
    let txQuery = `SELECT t.symbol, t.type, t.quantity, t.price, t.date, t.account_id
      FROM transactions t`;
    const params: (string | number)[] = [];
    if (account_id) {
      txQuery += ' WHERE t.account_id = ?';
      params.push(account_id as string);
    }
    txQuery += ' ORDER BY t.date ASC';
    const transactions = db.prepare(txQuery).all(...params) as Array<{ symbol: string; type: string; quantity: number; price: number; date: string; account_id: number }>;

    if (!transactions.length) {
      res.json([]);
      return;
    }

    // Get unique symbols that have positive holdings
    const holdingMap: Record<string, number> = {};
    for (const tx of transactions) {
      if (!holdingMap[tx.symbol]) holdingMap[tx.symbol] = 0;
      if (['buy', 'transfer_in', 'dividend'].includes(tx.type)) holdingMap[tx.symbol] += tx.quantity;
      else if (['sell', 'transfer_out'].includes(tx.type)) holdingMap[tx.symbol] -= tx.quantity;
    }
    const activeSymbols = Object.keys(holdingMap).filter(s => holdingMap[s] > 0.00000001);

    if (!activeSymbols.length) {
      res.json([]);
      return;
    }

    // Fetch price history for all active symbols
    const yf = await getYahooFinance();
    const priceHistories: Record<string, Record<string, number>> = {};
    const interval = days <= 30 ? '1d' : '1wk';

    for (const symbol of activeSymbols) {
      try {
        const result = await yf.chart(symbol, { period1: startDate, interval });
        priceHistories[symbol] = {};
        for (const q of (result.quotes || [])) {
          if (q.close !== null) {
            const dateKey = new Date(q.date).toISOString().split('T')[0];
            priceHistories[symbol][dateKey] = q.close;
          }
        }
      } catch {
        // Use last cached price as fallback
        const cached = db.prepare('SELECT price FROM price_cache WHERE symbol = ?').get(symbol) as { price: number } | undefined;
        priceHistories[symbol] = { fallback: cached?.price || 0 };
      }
    }

    // Get all unique dates from price histories
    const allDates = new Set<string>();
    for (const sym of Object.keys(priceHistories)) {
      for (const d of Object.keys(priceHistories[sym])) {
        if (d !== 'fallback') allDates.add(d);
      }
    }
    const sortedDates = [...allDates].sort();
    if (!sortedDates.length) {
      res.json([]);
      return;
    }

    // For each date, calculate portfolio value
    const portfolioHistory: Array<{ date: string; value: number; cost: number; gain: number }> = [];
    const runningHoldings: Record<string, number> = {};
    let txIndex = 0;

    for (const dateStr of sortedDates) {
      // Apply all transactions up to and including this date
      while (txIndex < transactions.length && transactions[txIndex].date <= dateStr) {
        const tx = transactions[txIndex];
        if (!runningHoldings[tx.symbol]) runningHoldings[tx.symbol] = 0;
        if (['buy', 'transfer_in', 'dividend'].includes(tx.type)) runningHoldings[tx.symbol] += tx.quantity;
        else if (['sell', 'transfer_out'].includes(tx.type)) runningHoldings[tx.symbol] -= tx.quantity;
        txIndex++;
      }

      // Calculate total portfolio value
      let totalValue = 0;
      let totalCost = 0;
      for (const [symbol, qty] of Object.entries(runningHoldings)) {
        if (qty <= 0.00000001) continue;
        const prices = priceHistories[symbol] || {};
        // Find closest price at or before this date
        let price = prices[dateStr];
        if (price === undefined) {
          // Find closest earlier date
          const priceDates = Object.keys(prices).filter(d => d !== 'fallback' && d <= dateStr).sort();
          price = priceDates.length ? prices[priceDates[priceDates.length - 1]] : (prices.fallback || 0);
        }
        totalValue += qty * price;
      }

      // Calculate cost basis up to this date
      for (const tx of transactions.filter(t => t.date <= dateStr)) {
        if (['buy', 'transfer_in'].includes(tx.type)) {
          totalCost += tx.quantity * tx.price;
        }
      }

      portfolioHistory.push({
        date: dateStr,
        value: Math.round(totalValue * 100) / 100,
        cost: Math.round(totalCost * 100) / 100,
        gain: Math.round((totalValue - totalCost) * 100) / 100
      });
    }

    res.json(portfolioHistory);
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
});

// Get daily wealth data for a date range (calendar view)
router.get('/daily-wealth', (req: AuthenticatedRequest, res: Response): void => {
  try {
    const { start, end } = req.query;
    if (!start || !end) {
      res.status(400).json({ error: 'start and end dates required (YYYY-MM-DD)' });
      return;
    }

    const rows = db.prepare(
      'SELECT date, total_wealth, total_cost, base_currency, details FROM daily_wealth WHERE date >= ? AND date <= ? ORDER BY date ASC'
    ).all(start, end) as Array<{ date: string; total_wealth: number; total_cost: number; base_currency: string; details: string }>;

    // Parse details JSON
    const result = rows.map(r => ({
      ...r,
      details: JSON.parse(r.details || '{}')
    }));

    res.json(result);
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
});

// Save/update daily wealth snapshot (called from dashboard summary to cache today's value)
router.post('/daily-wealth', async (_req: AuthenticatedRequest, res: Response): Promise<void> => {
  try {
    const today = new Date().toISOString().split('T')[0];

    // Get current dashboard summary data
    const holdings = db.prepare(`
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
    `).all() as Array<{ symbol: string; account_id: number; account_name: string; account_currency: string; quantity: number; avg_cost: number }>;

    const symbols = [...new Set(holdings.map(h => h.symbol))];
    const prices: Record<string, PriceCache> = {};
    for (const symbol of symbols) {
      const cached = db.prepare('SELECT * FROM price_cache WHERE symbol = ?').get(symbol) as PriceCache | undefined;
      if (cached) prices[symbol] = cached;
    }

    const rates: Record<string, number> = {};
    const rateRows = db.prepare('SELECT * FROM currency_rates').all() as CurrencyRate[];
    for (const r of rateRows) rates[`${r.from_currency}_${r.to_currency}`] = r.rate;

    const user = db.prepare('SELECT base_currency FROM users LIMIT 1').get() as User | undefined;
    const baseCurrency = user ? user.base_currency : 'EUR';

    let totalWealth = 0;
    let totalCost = 0;
    const accountDetails: Record<number, { name: string; market_value: number; cost_basis: number }> = {};

    for (const h of holdings) {
      const priceData = prices[h.symbol] || { price: 0, currency: 'USD' };
      let marketValue = h.quantity * priceData.price;
      let costBasis = h.quantity * (h.avg_cost || 0);

      const priceCurrency = priceData.currency || 'USD';
      if (priceCurrency !== baseCurrency) {
        const rateKey = `${priceCurrency}_${baseCurrency}`;
        marketValue *= (rates[rateKey] || 1);
      }
      if (h.account_currency && h.account_currency !== baseCurrency) {
        const rateKey = `${h.account_currency}_${baseCurrency}`;
        costBasis *= (rates[rateKey] || 1);
      }

      if (!accountDetails[h.account_id]) {
        accountDetails[h.account_id] = { name: h.account_name, market_value: 0, cost_basis: 0 };
      }
      accountDetails[h.account_id].market_value += marketValue;
      accountDetails[h.account_id].cost_basis += costBasis;
      totalWealth += marketValue;
      totalCost += costBasis;
    }

    const details = JSON.stringify({
      accounts: Object.entries(accountDetails).map(([id, d]) => ({
        account_id: parseInt(id), ...d
      })),
      holdings_count: holdings.length
    });

    db.prepare(`
      INSERT OR REPLACE INTO daily_wealth (date, total_wealth, total_cost, base_currency, details, updated_at)
      VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
    `).run(today, totalWealth, totalCost, baseCurrency, details);

    res.json({ date: today, total_wealth: totalWealth, total_cost: totalCost, base_currency: baseCurrency });
  } catch (e) {
    res.status(500).json({ error: (e as Error).message });
  }
});

export default router;
