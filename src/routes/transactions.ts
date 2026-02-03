import express, { Response } from 'express';
import multer from 'multer';
import { stringify } from 'csv-stringify/sync';
import { parse } from 'csv-parse/sync';
import db from '../db/database';
import { importCSV, detectFormat } from '../services/importer';
import { AuthenticatedRequest, Transaction, Tag, Account } from '../types';

const router = express.Router();
const upload = multer({ storage: multer.memoryStorage(), limits: { fileSize: 10 * 1024 * 1024 } });

interface TransactionWithAccount extends Transaction {
  account_name: string;
  tags?: Tag[];
}

function getTransactionWithTags(tx: Transaction | undefined): TransactionWithAccount | null {
  if (!tx) return null;
  const tags = db.prepare(
    'SELECT t.* FROM tags t JOIN transaction_tags tt ON t.id = tt.tag_id WHERE tt.transaction_id = ? ORDER BY t.name'
  ).all(tx.id) as Tag[];
  return { ...tx, tags } as TransactionWithAccount;
}

function syncTransactionTags(transactionId: number | bigint, tagIds: number[] | undefined): void {
  db.prepare('DELETE FROM transaction_tags WHERE transaction_id = ?').run(transactionId);
  if (tagIds && tagIds.length) {
    const insert = db.prepare('INSERT OR IGNORE INTO transaction_tags (transaction_id, tag_id) VALUES (?, ?)');
    for (const tagId of tagIds) {
      insert.run(transactionId, tagId);
    }
  }
}

router.get('/', (req: AuthenticatedRequest, res: Response): void => {
  const { account_id, symbol, limit, offset } = req.query;
  let sql = 'SELECT t.*, a.name as account_name FROM transactions t JOIN accounts a ON t.account_id = a.id WHERE 1=1';
  const params: (string | number)[] = [];

  if (account_id) { sql += ' AND t.account_id = ?'; params.push(account_id as string); }
  if (symbol) { sql += ' AND t.symbol = ?'; params.push(symbol as string); }

  sql += ' ORDER BY t.date DESC, t.id DESC';
  if (limit) { sql += ' LIMIT ?'; params.push(parseInt(limit as string)); }
  if (offset) { sql += ' OFFSET ?'; params.push(parseInt(offset as string)); }

  const transactions = db.prepare(sql).all(...params) as TransactionWithAccount[];
  const transactionsWithTags = transactions.map(tx => getTransactionWithTags(tx));
  res.json(transactionsWithTags);
});

router.get('/:id', (req: AuthenticatedRequest, res: Response): void => {
  if (req.params.id === 'export') return; // Let export route handle
  const tx = db.prepare('SELECT t.*, a.name as account_name FROM transactions t JOIN accounts a ON t.account_id = a.id WHERE t.id = ?').get(req.params.id) as TransactionWithAccount | undefined;
  if (!tx) {
    res.status(404).json({ error: 'Transaction not found' });
    return;
  }
  res.json(getTransactionWithTags(tx));
});

router.post('/', (req: AuthenticatedRequest, res: Response): void => {
  const { account_id, symbol, type, quantity, price, fee, currency, date, notes, tag_ids } = req.body;
  if (!account_id || !symbol || !type || !date) {
    res.status(400).json({ error: 'account_id, symbol, type, and date are required' });
    return;
  }

  const account = db.prepare('SELECT id FROM accounts WHERE id = ?').get(account_id) as Account | undefined;
  if (!account) {
    res.status(404).json({ error: 'Account not found' });
    return;
  }

  const result = db.prepare(`
    INSERT INTO transactions (account_id, symbol, type, quantity, price, fee, currency, date, notes)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `).run(account_id, (symbol as string).toUpperCase(), type, quantity || 0, price || 0, fee || 0, currency || 'EUR', date, notes || '');

  const transactionId = result.lastInsertRowid;
  if (tag_ids && tag_ids.length) syncTransactionTags(transactionId, tag_ids);

  const tx = getTransactionWithTags(db.prepare('SELECT * FROM transactions WHERE id = ?').get(transactionId) as Transaction);
  res.status(201).json(tx);
});

router.put('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM transactions WHERE id = ?').get(req.params.id) as Transaction | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Transaction not found' });
    return;
  }

  const { account_id, symbol, type, quantity, price, fee, currency, date, notes, tag_ids } = req.body;

  db.prepare(`
    UPDATE transactions SET account_id = ?, symbol = ?, type = ?, quantity = ?, price = ?, fee = ?, currency = ?, date = ?, notes = ?
    WHERE id = ?
  `).run(
    account_id || existing.account_id,
    ((symbol || existing.symbol) as string).toUpperCase(),
    type || existing.type,
    quantity !== undefined ? quantity : existing.quantity,
    price !== undefined ? price : existing.price,
    fee !== undefined ? fee : existing.fee,
    currency || existing.currency,
    date || existing.date,
    notes !== undefined ? notes : existing.notes,
    req.params.id
  );

  if (tag_ids !== undefined) syncTransactionTags(parseInt(req.params.id), tag_ids);

  const tx = getTransactionWithTags(db.prepare('SELECT * FROM transactions WHERE id = ?').get(req.params.id) as Transaction);
  res.json(tx);
});

router.delete('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM transactions WHERE id = ?').get(req.params.id) as Transaction | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Transaction not found' });
    return;
  }

  db.prepare('DELETE FROM transactions WHERE id = ?').run(req.params.id);
  res.json({ message: 'Transaction deleted' });
});

// Export transactions as CSV
router.get('/export/csv', (req: AuthenticatedRequest, res: Response): void => {
  const { account_id } = req.query;
  let sql = 'SELECT t.*, a.name as account_name FROM transactions t JOIN accounts a ON t.account_id = a.id';
  const params: string[] = [];

  if (account_id) { sql += ' WHERE t.account_id = ?'; params.push(account_id as string); }
  sql += ' ORDER BY t.date DESC';

  const transactions = db.prepare(sql).all(...params) as TransactionWithAccount[];

  const csv = stringify(transactions, {
    header: true,
    columns: ['id', 'account_name', 'symbol', 'type', 'quantity', 'price', 'fee', 'currency', 'date', 'notes']
  });

  res.setHeader('Content-Type', 'text/csv');
  res.setHeader('Content-Disposition', 'attachment; filename=transactions.csv');
  res.send(csv);
});

// Smart import - auto-detects format, deduplicates
router.post('/import/csv', upload.single('file'), (req: AuthenticatedRequest, res: Response): void => {
  if (!req.file) {
    res.status(400).json({ error: 'CSV file required' });
    return;
  }

  const { account_id, format } = req.body;
  if (!account_id) {
    res.status(400).json({ error: 'account_id required' });
    return;
  }

  const account = db.prepare('SELECT id FROM accounts WHERE id = ?').get(account_id) as Account | undefined;
  if (!account) {
    res.status(404).json({ error: 'Account not found' });
    return;
  }

  try {
    const result = importCSV(req.file.buffer.toString(), parseInt(account_id), format || null);
    res.json(result);
  } catch (e) {
    res.status(400).json({ error: 'Failed to import CSV: ' + (e as Error).message });
  }
});

// Detect CSV format without importing
router.post('/import/detect', upload.single('file'), (req: AuthenticatedRequest, res: Response): void => {
  if (!req.file) {
    res.status(400).json({ error: 'CSV file required' });
    return;
  }

  try {
    const records = parse(req.file.buffer.toString(), {
      columns: true,
      skip_empty_lines: true,
      trim: true,
      relax_quotes: true,
      relax_column_count: true,
      to: 1
    }) as Record<string, string>[];
    if (!records.length) {
      res.json({ format: 'unknown', headers: [] });
      return;
    }

    const headers = Object.keys(records[0]);
    const format = detectFormat(headers);
    res.json({ format, headers });
  } catch (e) {
    res.status(400).json({ error: 'Failed to parse CSV: ' + (e as Error).message });
  }
});

export default router;
