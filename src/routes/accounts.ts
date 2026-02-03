import express, { Response } from 'express';
import db from '../db/database';
import { AuthenticatedRequest, Account, Tag, Holding } from '../types';

const router = express.Router();

function getAccountWithTags(account: Account | undefined): (Account & { tags: Tag[] }) | null {
  if (!account) return null;
  const tags = db.prepare(
    'SELECT t.* FROM tags t JOIN account_tags at ON t.id = at.tag_id WHERE at.account_id = ? ORDER BY t.name'
  ).all(account.id) as Tag[];
  return { ...account, tags };
}

function syncAccountTags(accountId: number | bigint, tagIds: number[] | undefined): void {
  db.prepare('DELETE FROM account_tags WHERE account_id = ?').run(accountId);
  if (tagIds && tagIds.length) {
    const insert = db.prepare('INSERT OR IGNORE INTO account_tags (account_id, tag_id) VALUES (?, ?)');
    for (const tagId of tagIds) {
      insert.run(accountId, tagId);
    }
  }
}

router.get('/', (_req: AuthenticatedRequest, res: Response): void => {
  const accounts = db.prepare('SELECT * FROM accounts ORDER BY created_at DESC').all() as Account[];
  const accountsWithTags = accounts.map(a => getAccountWithTags(a));
  res.json(accountsWithTags);
});

// Purge all accounts, transactions, goals, and price cache
router.delete('/purge/all', (_req: AuthenticatedRequest, res: Response): void => {
  db.prepare('DELETE FROM transaction_tags').run();
  db.prepare('DELETE FROM transactions').run();
  db.prepare('DELETE FROM account_tags').run();
  db.prepare('DELETE FROM accounts').run();
  db.prepare('DELETE FROM goal_tags').run();
  db.prepare('DELETE FROM goals').run();
  db.prepare('DELETE FROM tags').run();
  db.prepare('DELETE FROM price_cache').run();
  res.json({ message: 'All accounts, transactions, goals, and cached prices have been purged.' });
});

router.get('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const account = db.prepare('SELECT * FROM accounts WHERE id = ?').get(req.params.id) as Account | undefined;
  if (!account) {
    res.status(404).json({ error: 'Account not found' });
    return;
  }
  res.json(getAccountWithTags(account));
});

router.post('/', (req: AuthenticatedRequest, res: Response): void => {
  const { name, type, currency, description, icon, color, tag_ids } = req.body;
  if (!name) {
    res.status(400).json({ error: 'Account name required' });
    return;
  }

  const result = db.prepare(`
    INSERT INTO accounts (name, type, currency, description, icon, color)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run(name, type || 'general', currency || 'EUR', description || '', icon || 'wallet', color || '#6366f1');

  const accountId = result.lastInsertRowid;
  if (tag_ids && tag_ids.length) syncAccountTags(accountId, tag_ids);

  const account = getAccountWithTags(db.prepare('SELECT * FROM accounts WHERE id = ?').get(accountId) as Account);
  res.status(201).json(account);
});

router.put('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const { name, type, currency, description, icon, color, tag_ids } = req.body;
  const existing = db.prepare('SELECT * FROM accounts WHERE id = ?').get(req.params.id) as Account | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Account not found' });
    return;
  }

  db.prepare(`
    UPDATE accounts SET name = ?, type = ?, currency = ?, description = ?, icon = ?, color = ?, updated_at = CURRENT_TIMESTAMP
    WHERE id = ?
  `).run(
    name || existing.name,
    type || existing.type,
    currency || existing.currency,
    description !== undefined ? description : existing.description,
    icon || existing.icon,
    color || existing.color,
    req.params.id
  );

  if (tag_ids !== undefined) syncAccountTags(parseInt(req.params.id), tag_ids);

  const account = getAccountWithTags(db.prepare('SELECT * FROM accounts WHERE id = ?').get(req.params.id) as Account);
  res.json(account);
});

router.delete('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM accounts WHERE id = ?').get(req.params.id) as Account | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Account not found' });
    return;
  }

  db.prepare('DELETE FROM accounts WHERE id = ?').run(req.params.id);
  res.json({ message: 'Account deleted' });
});

// Get holdings summary for an account (grouped by symbol)
router.get('/:id/holdings', (req: AuthenticatedRequest, res: Response): void => {
  const account = db.prepare('SELECT * FROM accounts WHERE id = ?').get(req.params.id) as Account | undefined;
  if (!account) {
    res.status(404).json({ error: 'Account not found' });
    return;
  }

  const holdings = db.prepare(`
    SELECT
      symbol,
      SUM(CASE WHEN type IN ('buy','transfer_in') THEN quantity ELSE 0 END) -
      SUM(CASE WHEN type IN ('sell','transfer_out') THEN quantity ELSE 0 END) as quantity,
      SUM(CASE WHEN type IN ('buy','transfer_in') THEN quantity * price ELSE 0 END) /
      NULLIF(SUM(CASE WHEN type IN ('buy','transfer_in') THEN quantity ELSE 0 END), 0) as avg_cost,
      SUM(CASE WHEN type = 'buy' THEN quantity * price + fee WHEN type = 'sell' THEN -(quantity * price - fee) ELSE 0 END) as total_cost,
      COUNT(*) as transaction_count,
      MIN(date) as first_transaction,
      MAX(date) as last_transaction
    FROM transactions
    WHERE account_id = ?
    GROUP BY symbol
    HAVING quantity > 0.00000001
    ORDER BY total_cost DESC
  `).all(req.params.id) as Holding[];

  res.json(holdings);
});

export default router;
