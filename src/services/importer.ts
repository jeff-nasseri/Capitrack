import { parse } from 'csv-parse/sync';
import fs from 'fs';
import path from 'path';
import db from '../db/database';
import { CSVFormat, ImportResult, ImportedTransaction, Transaction, Account } from '../types';

// ===== Format Detection =====
export function detectFormat(headers: string[]): CSVFormat {
  const h = headers.map(s => s.toLowerCase().trim());
  if (h.includes('ticker') && h.includes('price per share')) return 'revolut-stocks';
  if (h.includes('product') && h.includes('started date') && h.includes('state')) return 'revolut-commodities';
  if (h.includes('transaction id') && h.includes('amount unit')) return 'trezor';
  if (h.includes('symbol') && h.includes('type')) return 'generic';
  return 'unknown';
}

// ===== Deduplication =====
interface TransactionFingerprint {
  account_id: number;
  symbol: string;
  type: string;
  quantity: number;
  price: number;
  date: string;
}

function buildFingerprint(tx: TransactionFingerprint): string {
  // Unique key: account_id + symbol + type + quantity + price + date (trimmed to day)
  const datePart = (tx.date || '').split('T')[0].split(' ')[0];
  return `${tx.account_id}|${tx.symbol}|${tx.type}|${Number(tx.quantity).toFixed(8)}|${Number(tx.price).toFixed(4)}|${datePart}`;
}

function getExistingFingerprints(accountId: number): Set<string> {
  const rows = db.prepare(`
    SELECT account_id, symbol, type, quantity, price, date FROM transactions WHERE account_id = ?
  `).all(accountId) as TransactionFingerprint[];
  const set = new Set<string>();
  for (const r of rows) {
    set.add(buildFingerprint(r));
  }
  return set;
}

// ===== Revolut Stock Parser =====
function parseRevolutStock(records: Record<string, string>[], _accountId: number): ImportedTransaction[] {
  const transactions: ImportedTransaction[] = [];
  for (const r of records) {
    const type = (r['Type'] || '').trim();
    const ticker = (r['Ticker'] || '').trim();

    // Skip non-trade rows
    if (!ticker || ['CASH TOP-UP', 'CASH WITHDRAWAL'].includes(type)) continue;

    let txType: Transaction['type'];
    if (type === 'BUY - MARKET') txType = 'buy';
    else if (type === 'SELL - MARKET') txType = 'sell';
    else if (type === 'DIVIDEND') txType = 'dividend';
    else if (type === 'STOCK SPLIT') txType = 'transfer_in'; // stock split = free shares
    else continue;

    const quantity = Math.abs(parseFloat(r['Quantity'] || '0'));
    const priceStr = (r['Price per share'] || '0').replace(/[^0-9.\-]/g, '');
    const price = parseFloat(priceStr) || 0;
    const totalStr = (r['Total Amount'] || '0').replace(/[^0-9.\-]/g, '');
    const total = Math.abs(parseFloat(totalStr)) || 0;
    const currency = (r['Currency'] || 'USD').trim();
    const dateStr = (r['Date'] || '').trim();
    const date = dateStr ? new Date(dateStr).toISOString().split('T')[0] : '';

    // For dividends, the amount is in Total Amount, no quantity/price
    let finalQty = quantity;
    let finalPrice = price;
    if (txType === 'dividend') {
      finalQty = total;
      finalPrice = 1;
    }
    if (txType === 'transfer_in' && quantity === 0 && total === 0) {
      // Stock split with 0 value — still record the quantity
      finalQty = parseFloat(r['Quantity'] || '0');
      finalPrice = 0;
    }

    if (!date) continue;

    transactions.push({
      symbol: ticker.toUpperCase(),
      type: txType,
      quantity: finalQty,
      price: finalPrice,
      fee: 0,
      currency,
      date,
      notes: `Revolut: ${type}`
    });
  }
  return transactions;
}

// ===== Revolut Commodity Parser =====
function parseRevolutCommodity(records: Record<string, string>[], _accountId: number): ImportedTransaction[] {
  const transactions: ImportedTransaction[] = [];
  for (const r of records) {
    const state = (r['State'] || '').trim();
    if (state !== 'COMPLETED') continue;

    const description = (r['Description'] || '').trim();
    const amount = parseFloat(r['Amount'] || '0');
    const fee = Math.abs(parseFloat(r['Fee'] || '0'));
    const currency = (r['Currency'] || 'XAU').trim();
    const dateStr = (r['Started Date'] || r['Completed Date'] || '').trim();
    const date = dateStr ? dateStr.split(' ')[0] : '';

    if (!date) continue;

    // Map commodity currencies to Yahoo Finance symbols
    const symbolMap: Record<string, string> = { 'XAU': 'GC=F', 'XAG': 'SI=F', 'XPT': 'PL=F', 'XPD': 'PA=F' };
    const symbol = symbolMap[currency] || currency;

    let txType: Transaction['type'];
    if (description.includes('Exchanged to EUR') || description.includes('Exchanged to USD')) {
      // Selling commodity for fiat
      txType = 'sell';
    } else if (description.startsWith('Exchanged to')) {
      // Buying commodity
      txType = 'buy';
    } else {
      continue;
    }

    const quantity = Math.abs(amount);

    transactions.push({
      symbol,
      type: txType,
      quantity,
      price: 0, // Price not in CSV, will use market price at the time
      fee,
      currency: 'EUR',
      date,
      notes: `Revolut Commodity: ${description} (${currency})`
    });
  }
  return transactions;
}

// ===== Trezor BTC Parser =====
function parseTrezor(records: Record<string, string>[], _accountId: number): ImportedTransaction[] {
  const transactions: ImportedTransaction[] = [];
  for (const r of records) {
    const type = (r['Type'] || '').trim().toUpperCase();
    const amount = Math.abs(parseFloat(r['Amount'] || '0'));
    const amountUnit = (r['Amount unit'] || 'BTC').trim();
    const fiatStr = (r['Fiat (USD)'] || '0').replace(/[^0-9.\-]/g, '');
    const fiatUsd = Math.abs(parseFloat(fiatStr)) || 0;
    const feeStr = (r['Fee'] || '0').replace(/[^0-9.\-]/g, '');
    const fee = Math.abs(parseFloat(feeStr)) || 0;
    const dateStr = (r['Date'] || '').trim();
    const txId = (r['Transaction ID'] || '').trim();

    // Parse date from M/D/YYYY format
    let date = '';
    if (dateStr) {
      const parts = dateStr.split('/');
      if (parts.length === 3) {
        const [month, day, year] = parts;
        date = `${year}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`;
      }
    }

    if (!date || !amount) continue;

    // Map amount unit to Yahoo Finance symbol
    const symbolMap: Record<string, string> = { 'BTC': 'BTC-USD', 'ETH': 'ETH-USD', 'LTC': 'LTC-USD' };
    const symbol = symbolMap[amountUnit] || `${amountUnit}-USD`;

    let txType: Transaction['type'];
    if (type === 'RECV') txType = 'transfer_in';
    else if (type === 'SENT') txType = 'transfer_out';
    else continue;

    // Calculate price from fiat value
    const price = amount > 0 ? fiatUsd / amount : 0;

    transactions.push({
      symbol,
      type: txType,
      quantity: amount,
      price,
      fee,
      currency: 'USD',
      date,
      notes: txId ? `TxID: ${txId.substring(0, 16)}...` : `Trezor ${amountUnit}`
    });
  }
  return transactions;
}

// ===== Generic CSV Parser =====
function parseGeneric(records: Record<string, string>[], _accountId: number): ImportedTransaction[] {
  const transactions: ImportedTransaction[] = [];
  for (const r of records) {
    const symbol = (r.symbol || r.Symbol || r.SYMBOL || '').toUpperCase();
    const type = (r.type || r.Type || r.TYPE || 'buy').toLowerCase() as Transaction['type'];
    const quantity = parseFloat(r.quantity || r.Quantity || r.QUANTITY || '0');
    const price = parseFloat(r.price || r.Price || r.PRICE || '0');
    const fee = parseFloat(r.fee || r.Fee || r.FEE || '0');
    const currency = r.currency || r.Currency || r.CURRENCY || 'EUR';
    const date = r.date || r.Date || r.DATE || '';
    const notes = r.notes || r.Notes || r.NOTES || '';

    if (!symbol || !date) continue;
    if (!['buy', 'sell', 'transfer_in', 'transfer_out', 'dividend', 'interest', 'fee'].includes(type)) continue;

    transactions.push({
      symbol, type, quantity, price, fee, currency, date, notes
    });
  }
  return transactions;
}

// ===== Main Import Function =====
export function importCSV(csvContent: string, accountId: number, formatHint: CSVFormat | null): ImportResult & { format: CSVFormat; total: number } {
  const records = parse(csvContent, {
    columns: true,
    skip_empty_lines: true,
    trim: true,
    relax_quotes: true,
    relax_column_count: true
  }) as Record<string, string>[];

  if (!records.length) return { imported: 0, skipped: 0, total: 0, errors: [], format: 'unknown' };

  // Detect format
  const headers = Object.keys(records[0]);
  const format = formatHint || detectFormat(headers);

  let parsedTx: ImportedTransaction[];
  switch (format) {
    case 'revolut-stocks': parsedTx = parseRevolutStock(records, accountId); break;
    case 'revolut-commodities': parsedTx = parseRevolutCommodity(records, accountId); break;
    case 'trezor': parsedTx = parseTrezor(records, accountId); break;
    case 'generic': parsedTx = parseGeneric(records, accountId); break;
    default: return { imported: 0, skipped: 0, total: records.length, errors: [`Unknown CSV format. Headers: ${headers.join(', ')}`], format: 'unknown' };
  }

  // Deduplication
  const existing = getExistingFingerprints(accountId);

  const insert = db.prepare(`
    INSERT INTO transactions (account_id, symbol, type, quantity, price, fee, currency, date, notes)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `);

  let imported = 0;
  let skipped = 0;
  const errors: string[] = [];

  const doImport = db.transaction(() => {
    for (let i = 0; i < parsedTx.length; i++) {
      const tx = parsedTx[i];
      try {
        const fp = buildFingerprint({ account_id: accountId, ...tx });
        if (existing.has(fp)) {
          skipped++;
          continue;
        }

        insert.run(accountId, tx.symbol, tx.type, tx.quantity, tx.price, tx.fee, tx.currency, tx.date, tx.notes);
        existing.add(fp); // Prevent duplicates within same file
        imported++;
      } catch (e) {
        errors.push(`Row ${i + 1}: ${(e as Error).message}`);
      }
    }
  });

  doImport();

  return { imported, skipped, total: parsedTx.length, errors, format };
}

// ===== Auto-Import from Transactions Folder =====
export function autoImportFromFolder(transactionsDir: string): void {
  if (!fs.existsSync(transactionsDir)) {
    console.log('[auto-import] No transactions directory found at', transactionsDir);
    return;
  }

  const accountMappings: Record<string, { name: string; type: string; icon: string; color: string }> = {
    'revolut-stocks': { name: 'Stock Portfolio', type: 'stock', icon: 'chart-line', color: '#10b981' },
    'revolut-commodities': { name: 'Commodities', type: 'commodity', icon: 'gem', color: '#8b5cf6' },
    'trezor': { name: 'Crypto Portfolio', type: 'crypto', icon: 'bitcoin', color: '#f59e0b' }
  };

  // Find all CSV files recursively
  const csvFiles = findCSVFiles(transactionsDir);
  console.log(`[auto-import] Found ${csvFiles.length} CSV file(s) in ${transactionsDir}`);

  for (const filePath of csvFiles) {
    try {
      const content = fs.readFileSync(filePath, 'utf-8');
      const records = parse(content, { columns: true, skip_empty_lines: true, trim: true, relax_quotes: true, relax_column_count: true }) as Record<string, string>[];
      if (!records.length) continue;

      const headers = Object.keys(records[0]);
      const format = detectFormat(headers);

      if (format === 'unknown') {
        console.log(`[auto-import] Skipping ${filePath} - unknown format`);
        continue;
      }

      // Find or create account
      const mapping = accountMappings[format] || { name: 'Imported', type: 'general', icon: 'wallet', color: '#6366f1' };
      let account = db.prepare('SELECT * FROM accounts WHERE name = ? AND type = ?').get(mapping.name, mapping.type) as Account | undefined;
      if (!account) {
        const result = db.prepare('INSERT INTO accounts (name, type, currency, description, icon, color) VALUES (?, ?, ?, ?, ?, ?)')
          .run(mapping.name, mapping.type, 'EUR', `Auto-imported from ${format}`, mapping.icon, mapping.color);
        account = db.prepare('SELECT * FROM accounts WHERE id = ?').get(result.lastInsertRowid) as Account;
        console.log(`[auto-import] Created account: ${mapping.name}`);
      }

      const result = importCSV(content, account.id, format);
      console.log(`[auto-import] ${path.basename(filePath)}: imported=${result.imported}, skipped=${result.skipped}, format=${result.format}`);
      if (result.errors.length) {
        console.log(`[auto-import] Errors:`, result.errors.slice(0, 5));
      }
    } catch (e) {
      console.error(`[auto-import] Error processing ${filePath}:`, (e as Error).message);
    }
  }
}

function findCSVFiles(dir: string): string[] {
  const results: string[] = [];
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...findCSVFiles(fullPath));
    } else if (entry.name.toLowerCase().endsWith('.csv')) {
      results.push(fullPath);
    }
  }
  return results;
}
