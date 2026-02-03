import express, { Response } from 'express';
import db from '../db/database';
import { AuthenticatedRequest, Tag } from '../types';

const router = express.Router();

router.get('/', (_req: AuthenticatedRequest, res: Response): void => {
  const tags = db.prepare('SELECT * FROM tags ORDER BY name ASC').all() as Tag[];
  res.json(tags);
});

router.get('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const tag = db.prepare('SELECT * FROM tags WHERE id = ?').get(req.params.id) as Tag | undefined;
  if (!tag) {
    res.status(404).json({ error: 'Tag not found' });
    return;
  }
  res.json(tag);
});

router.post('/', (req: AuthenticatedRequest, res: Response): void => {
  const { name, color } = req.body;
  if (!name) {
    res.status(400).json({ error: 'Tag name required' });
    return;
  }

  const existing = db.prepare('SELECT * FROM tags WHERE name = ?').get(name) as Tag | undefined;
  if (existing) {
    res.status(400).json({ error: 'Tag already exists' });
    return;
  }

  const result = db.prepare('INSERT INTO tags (name, color) VALUES (?, ?)').run(name, color || '#6366f1');
  const tag = db.prepare('SELECT * FROM tags WHERE id = ?').get(result.lastInsertRowid) as Tag;
  res.status(201).json(tag);
});

router.put('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM tags WHERE id = ?').get(req.params.id) as Tag | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Tag not found' });
    return;
  }

  const { name, color } = req.body;

  if (name && name !== existing.name) {
    const duplicate = db.prepare('SELECT * FROM tags WHERE name = ? AND id != ?').get(name, req.params.id) as Tag | undefined;
    if (duplicate) {
      res.status(400).json({ error: 'Tag name already exists' });
      return;
    }
  }

  db.prepare('UPDATE tags SET name = ?, color = ? WHERE id = ?').run(
    name || existing.name,
    color || existing.color,
    req.params.id
  );

  const tag = db.prepare('SELECT * FROM tags WHERE id = ?').get(req.params.id) as Tag;
  res.json(tag);
});

router.delete('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM tags WHERE id = ?').get(req.params.id) as Tag | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Tag not found' });
    return;
  }

  db.prepare('DELETE FROM tags WHERE id = ?').run(req.params.id);
  res.json({ message: 'Tag deleted' });
});

export default router;
