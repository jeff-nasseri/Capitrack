import express, { Response } from 'express';
import db from '../db/database';
import { AuthenticatedRequest, CurrencyRate } from '../types';

const router = express.Router();

router.get('/', (_req: AuthenticatedRequest, res: Response): void => {
  const rates = db.prepare('SELECT * FROM currency_rates ORDER BY from_currency, to_currency').all() as CurrencyRate[];
  res.json(rates);
});

router.post('/', (req: AuthenticatedRequest, res: Response): void => {
  const { from_currency, to_currency, rate } = req.body;
  if (!from_currency || !to_currency || rate === undefined) {
    res.status(400).json({ error: 'from_currency, to_currency, and rate required' });
    return;
  }

  db.prepare(`
    INSERT OR REPLACE INTO currency_rates (from_currency, to_currency, rate, updated_at)
    VALUES (?, ?, ?, CURRENT_TIMESTAMP)
  `).run((from_currency as string).toUpperCase(), (to_currency as string).toUpperCase(), rate);

  const saved = db.prepare('SELECT * FROM currency_rates WHERE from_currency = ? AND to_currency = ?')
    .get((from_currency as string).toUpperCase(), (to_currency as string).toUpperCase()) as CurrencyRate;
  res.status(201).json(saved);
});

router.put('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM currency_rates WHERE id = ?').get(req.params.id) as CurrencyRate | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Rate not found' });
    return;
  }

  const { from_currency, to_currency, rate } = req.body;

  db.prepare(`
    UPDATE currency_rates SET from_currency = ?, to_currency = ?, rate = ?, updated_at = CURRENT_TIMESTAMP
    WHERE id = ?
  `).run(
    ((from_currency || existing.from_currency) as string).toUpperCase(),
    ((to_currency || existing.to_currency) as string).toUpperCase(),
    rate !== undefined ? rate : existing.rate,
    req.params.id
  );

  const saved = db.prepare('SELECT * FROM currency_rates WHERE id = ?').get(req.params.id) as CurrencyRate;
  res.json(saved);
});

router.delete('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM currency_rates WHERE id = ?').get(req.params.id) as CurrencyRate | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Rate not found' });
    return;
  }

  db.prepare('DELETE FROM currency_rates WHERE id = ?').run(req.params.id);
  res.json({ message: 'Rate deleted' });
});

// Convert amount between currencies
router.get('/convert', (req: AuthenticatedRequest, res: Response): void => {
  const { from, to, amount } = req.query;
  if (!from || !to || !amount) {
    res.status(400).json({ error: 'from, to, and amount required' });
    return;
  }

  if ((from as string).toUpperCase() === (to as string).toUpperCase()) {
    res.json({ result: parseFloat(amount as string), rate: 1 });
    return;
  }

  const rate = db.prepare('SELECT rate FROM currency_rates WHERE from_currency = ? AND to_currency = ?')
    .get((from as string).toUpperCase(), (to as string).toUpperCase()) as { rate: number } | undefined;

  if (!rate) {
    res.status(404).json({ error: `No rate found for ${from} to ${to}` });
    return;
  }

  res.json({ result: parseFloat(amount as string) * rate.rate, rate: rate.rate });
});

export default router;
