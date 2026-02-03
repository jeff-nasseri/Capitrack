import express, { Response } from 'express';
import db from '../db/database';
import { AuthenticatedRequest, Goal, Tag, YearProgress, QuarterProgress, MonthProgress, WeekProgress } from '../types';

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
  const { year, quarter, month, week, category_id, tag_id, status } = req.query;
  let sql = 'SELECT * FROM goals WHERE 1=1';
  const params: (string | number)[] = [];

  if (year) { sql += ' AND year = ?'; params.push(parseInt(year as string)); }
  if (quarter) { sql += ' AND quarter = ?'; params.push(parseInt(quarter as string)); }
  if (month) { sql += ' AND month = ?'; params.push(parseInt(month as string)); }
  if (week) { sql += ' AND week = ?'; params.push(parseInt(week as string)); }
  if (category_id) { sql += ' AND category_id = ?'; params.push(parseInt(category_id as string)); }
  if (status) { sql += ' AND status = ?'; params.push(status as string); }

  sql += ' ORDER BY year ASC, quarter ASC, month ASC, week ASC, target_date ASC';

  let goals = db.prepare(sql).all(...params) as Goal[];

  if (tag_id) {
    const goalIds = (db.prepare('SELECT goal_id FROM goal_tags WHERE tag_id = ?').all(parseInt(tag_id as string)) as { goal_id: number }[]).map(r => r.goal_id);
    goals = goals.filter(g => goalIds.includes(g.id));
  }

  const goalsWithTags = goals.map(g => getGoalWithTags(g));
  res.json(goalsWithTags);
});

router.get('/progress', (req: AuthenticatedRequest, res: Response): void => {
  const { year } = req.query;
  const currentYear = year ? parseInt(year as string) : new Date().getFullYear();

  const yearGoals = db.prepare('SELECT * FROM goals WHERE year = ?').all(currentYear) as Goal[];
  const yearTotal = yearGoals.length;
  const yearCompleted = yearGoals.filter(g => g.status === 'completed' || g.achieved).length;

  const quarters: QuarterProgress[] = [];
  for (let q = 1; q <= 4; q++) {
    const qGoals = yearGoals.filter(g => g.quarter === q);
    const qTotal = qGoals.length;
    const qCompleted = qGoals.filter(g => g.status === 'completed' || g.achieved).length;

    const months: MonthProgress[] = [];
    const startMonth = (q - 1) * 3 + 1;
    for (let m = startMonth; m < startMonth + 3; m++) {
      const mGoals = qGoals.filter(g => g.month === m);
      const mTotal = mGoals.length;
      const mCompleted = mGoals.filter(g => g.status === 'completed' || g.achieved).length;

      const weeks: WeekProgress[] = [];
      for (let w = 1; w <= 4; w++) {
        const wGoals = mGoals.filter(g => g.week === w);
        const wTotal = wGoals.length;
        const wCompleted = wGoals.filter(g => g.status === 'completed' || g.achieved).length;
        weeks.push({ week: w, total: wTotal, completed: wCompleted, progress: wTotal > 0 ? Math.round((wCompleted / wTotal) * 100) : 0 });
      }

      months.push({ month: m, total: mTotal, completed: mCompleted, progress: mTotal > 0 ? Math.round((mCompleted / mTotal) * 100) : 0, weeks });
    }

    quarters.push({ quarter: q, total: qTotal, completed: qCompleted, progress: qTotal > 0 ? Math.round((qCompleted / qTotal) * 100) : 0, months });
  }

  const result: YearProgress = {
    year: currentYear,
    total: yearTotal,
    completed: yearCompleted,
    progress: yearTotal > 0 ? Math.round((yearCompleted / yearTotal) * 100) : 0,
    quarters
  };

  res.json(result);
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
  const { title, target_amount, current_amount, currency, target_date, description, category_id, year, quarter, month, week, status, tag_ids } = req.body;
  if (!title || !target_date) {
    res.status(400).json({ error: 'Title and target date required' });
    return;
  }

  const result = db.prepare(`
    INSERT INTO goals (title, target_amount, current_amount, currency, target_date, description, category_id, year, quarter, month, week, status)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `).run(
    title,
    target_amount || 0,
    current_amount || 0,
    currency || 'EUR',
    target_date,
    description || '',
    category_id || null,
    year || null,
    quarter || null,
    month || null,
    week || null,
    status || 'not_started'
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

  const { title, target_amount, current_amount, currency, target_date, description, achieved, category_id, year, quarter, month, week, status, tag_ids } = req.body;

  db.prepare(`
    UPDATE goals SET title = ?, target_amount = ?, current_amount = ?, currency = ?, target_date = ?, description = ?, achieved = ?, category_id = ?, year = ?, quarter = ?, month = ?, week = ?, status = ?, updated_at = CURRENT_TIMESTAMP
    WHERE id = ?
  `).run(
    title || existing.title,
    target_amount !== undefined ? target_amount : existing.target_amount,
    current_amount !== undefined ? current_amount : existing.current_amount,
    currency || existing.currency,
    target_date || existing.target_date,
    description !== undefined ? description : existing.description,
    achieved !== undefined ? (achieved ? 1 : 0) : existing.achieved,
    category_id !== undefined ? (category_id || null) : existing.category_id,
    year !== undefined ? year : existing.year,
    quarter !== undefined ? quarter : existing.quarter,
    month !== undefined ? month : existing.month,
    week !== undefined ? week : existing.week,
    status || existing.status,
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
