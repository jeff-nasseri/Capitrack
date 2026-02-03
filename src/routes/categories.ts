import express, { Response } from 'express';
import db from '../db/database';
import { AuthenticatedRequest, Category } from '../types';

const router = express.Router();

interface CategoryWithChildren extends Category {
  children: CategoryWithChildren[];
}

router.get('/', (_req: AuthenticatedRequest, res: Response): void => {
  const categories = db.prepare('SELECT * FROM categories ORDER BY name ASC').all() as Category[];
  res.json(categories);
});

router.get('/tree', (_req: AuthenticatedRequest, res: Response): void => {
  const categories = db.prepare('SELECT * FROM categories ORDER BY name ASC').all() as Category[];
  const map: Record<number, CategoryWithChildren> = {};
  const roots: CategoryWithChildren[] = [];

  for (const c of categories) {
    map[c.id] = { ...c, children: [] };
  }
  for (const c of categories) {
    if (c.parent_id && map[c.parent_id]) {
      map[c.parent_id].children.push(map[c.id]);
    } else {
      roots.push(map[c.id]);
    }
  }
  res.json(roots);
});

router.get('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const category = db.prepare('SELECT * FROM categories WHERE id = ?').get(req.params.id) as Category | undefined;
  if (!category) {
    res.status(404).json({ error: 'Category not found' });
    return;
  }
  res.json(category);
});

router.post('/', (req: AuthenticatedRequest, res: Response): void => {
  const { name, parent_id, color, icon } = req.body;
  if (!name) {
    res.status(400).json({ error: 'Category name required' });
    return;
  }

  if (parent_id) {
    const parent = db.prepare('SELECT * FROM categories WHERE id = ?').get(parent_id) as Category | undefined;
    if (!parent) {
      res.status(400).json({ error: 'Parent category not found' });
      return;
    }
  }

  const result = db.prepare(
    'INSERT INTO categories (name, parent_id, color, icon) VALUES (?, ?, ?, ?)'
  ).run(name, parent_id || null, color || '#6366f1', icon || 'folder');

  const category = db.prepare('SELECT * FROM categories WHERE id = ?').get(result.lastInsertRowid) as Category;
  res.status(201).json(category);
});

router.put('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM categories WHERE id = ?').get(req.params.id) as Category | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Category not found' });
    return;
  }

  const { name, parent_id, color, icon } = req.body;

  if (parent_id !== undefined && parent_id !== null) {
    if (parseInt(parent_id) === existing.id) {
      res.status(400).json({ error: 'Category cannot be its own parent' });
      return;
    }
    const parent = db.prepare('SELECT * FROM categories WHERE id = ?').get(parent_id) as Category | undefined;
    if (!parent) {
      res.status(400).json({ error: 'Parent category not found' });
      return;
    }
  }

  db.prepare(
    'UPDATE categories SET name = ?, parent_id = ?, color = ?, icon = ? WHERE id = ?'
  ).run(
    name || existing.name,
    parent_id !== undefined ? (parent_id || null) : existing.parent_id,
    color || existing.color,
    icon || existing.icon,
    req.params.id
  );

  const category = db.prepare('SELECT * FROM categories WHERE id = ?').get(req.params.id) as Category;
  res.json(category);
});

router.delete('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM categories WHERE id = ?').get(req.params.id) as Category | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Category not found' });
    return;
  }

  db.prepare('DELETE FROM categories WHERE id = ?').run(req.params.id);
  res.json({ message: 'Category deleted' });
});

export default router;
