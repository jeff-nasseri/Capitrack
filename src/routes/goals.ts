import express, { Response } from 'express';
import db from '../db/database';
import { AuthenticatedRequest, Goal, Tag } from '../types';

const router = express.Router();

function getGoalWithTags(goal: Goal | undefined): (Goal & { tags: Tag[] }) | null {
  if (!goal) return null;
  const tags = db.prepare(
    'SELECT t.* FROM tags t JOIN goal_tags gt ON t.id = gt.tag_id WHERE gt.goal_id = ? ORDER BY t.name'
  ).all(goal.id) as Tag[];
  return { ...goal, tags };
}

function syncGoalTags(goalId: number | bigint, tagIds: number[] | undefined): void {
  db.prepare('DELETE FROM goal_tags WHERE goal_id = ?').run(goalId);
  if (tagIds && tagIds.length) {
    const insert = db.prepare('INSERT OR IGNORE INTO goal_tags (goal_id, tag_id) VALUES (?, ?)');
    for (const tagId of tagIds) {
      insert.run(goalId, tagId);
    }
  }
}

router.get('/', (req: AuthenticatedRequest, res: Response): void => {
  const { category_id, tag_id } = req.query;
  let sql = 'SELECT * FROM goals WHERE 1=1';
  const params: (string | number)[] = [];

  if (category_id) { sql += ' AND category_id = ?'; params.push(parseInt(category_id as string)); }

  sql += ' ORDER BY target_date ASC';

  let goals = db.prepare(sql).all(...params) as Goal[];

  if (tag_id) {
    const goalIds = (db.prepare('SELECT goal_id FROM goal_tags WHERE tag_id = ?').all(parseInt(tag_id as string)) as { goal_id: number }[]).map(r => r.goal_id);
    goals = goals.filter(g => goalIds.includes(g.id));
  }

  const goalsWithTags = goals.map(g => getGoalWithTags(g));
  res.json(goalsWithTags);
});

router.get('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const goal = db.prepare('SELECT * FROM goals WHERE id = ?').get(req.params.id) as Goal | undefined;
  if (!goal) {
    res.status(404).json({ error: 'Goal not found' });
    return;
  }
  res.json(getGoalWithTags(goal));
});

router.post('/', (req: AuthenticatedRequest, res: Response): void => {
  const { title, target_amount, target_date, description, category_id, tag_ids } = req.body;
  if (!title || !target_date) {
    res.status(400).json({ error: 'Title and target date required' });
    return;
  }

  const result = db.prepare(`
    INSERT INTO goals (title, target_amount, target_date, description, category_id)
    VALUES (?, ?, ?, ?, ?)
  `).run(
    title,
    target_amount || 0,
    target_date,
    description || '',
    category_id || null
  );

  const goalId = result.lastInsertRowid;
  if (tag_ids && tag_ids.length) syncGoalTags(goalId, tag_ids);

  const goal = getGoalWithTags(db.prepare('SELECT * FROM goals WHERE id = ?').get(goalId) as Goal);
  res.status(201).json(goal);
});

router.put('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM goals WHERE id = ?').get(req.params.id) as Goal | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Goal not found' });
    return;
  }

  const { title, target_amount, target_date, description, achieved, category_id, tag_ids } = req.body;

  db.prepare(`
    UPDATE goals SET title = ?, target_amount = ?, target_date = ?, description = ?, achieved = ?, category_id = ?, updated_at = CURRENT_TIMESTAMP
    WHERE id = ?
  `).run(
    title || existing.title,
    target_amount !== undefined ? target_amount : existing.target_amount,
    target_date || existing.target_date,
    description !== undefined ? description : existing.description,
    achieved !== undefined ? (achieved ? 1 : 0) : existing.achieved,
    category_id !== undefined ? (category_id || null) : existing.category_id,
    req.params.id
  );

  if (tag_ids !== undefined) syncGoalTags(parseInt(req.params.id), tag_ids);

  const goal = getGoalWithTags(db.prepare('SELECT * FROM goals WHERE id = ?').get(req.params.id) as Goal);
  res.json(goal);
});

router.delete('/:id', (req: AuthenticatedRequest, res: Response): void => {
  const existing = db.prepare('SELECT * FROM goals WHERE id = ?').get(req.params.id) as Goal | undefined;
  if (!existing) {
    res.status(404).json({ error: 'Goal not found' });
    return;
  }

  db.prepare('DELETE FROM goals WHERE id = ?').run(req.params.id);
  res.json({ message: 'Goal deleted' });
});

router.delete('/', (_req: AuthenticatedRequest, res: Response): void => {
  db.prepare('DELETE FROM goal_tags').run();
  db.prepare('DELETE FROM goals').run();
  res.json({ message: 'All goals deleted' });
});

export default router;
