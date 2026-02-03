import express, { Response } from 'express';
import bcrypt from 'bcryptjs';
import db from '../db/database';
import { requireAuth } from '../middleware/auth';
import { AuthenticatedRequest, User } from '../types';

const router = express.Router();

router.post('/login', (req: AuthenticatedRequest, res: Response): void => {
  const { username, password } = req.body;
  if (!username || !password) {
    res.status(400).json({ error: 'Username and password required' });
    return;
  }

  const user = db.prepare('SELECT * FROM users WHERE username = ?').get(username) as User | undefined;
  if (!user || !bcrypt.compareSync(password, user.password_hash)) {
    res.status(401).json({ error: 'Invalid credentials' });
    return;
  }

  req.session.userId = user.id;
  req.session.username = user.username;
  res.json({ username: user.username, base_currency: user.base_currency });
});

router.post('/logout', (req: AuthenticatedRequest, res: Response): void => {
  req.session.destroy(() => {
    res.json({ message: 'Logged out' });
  });
});

router.get('/session', (req: AuthenticatedRequest, res: Response): void => {
  if (!req.session || !req.session.userId) {
    res.status(401).json({ error: 'Not authenticated' });
    return;
  }
  const user = db.prepare('SELECT username, base_currency FROM users WHERE id = ?').get(req.session.userId) as { username: string; base_currency: string } | undefined;
  res.json(user);
});

router.put('/password', requireAuth, (req: AuthenticatedRequest, res: Response): void => {
  const { currentPassword, newPassword } = req.body;
  if (!currentPassword || !newPassword) {
    res.status(400).json({ error: 'Current and new password required' });
    return;
  }

  if (newPassword.length < 8 || !/[A-Z]/.test(newPassword) || !/[a-z]/.test(newPassword) || !/[0-9]/.test(newPassword) || !/[!@#$%^&*]/.test(newPassword)) {
    res.status(400).json({ error: 'Password must be at least 8 characters with uppercase, lowercase, number, and special character' });
    return;
  }

  const user = db.prepare('SELECT * FROM users WHERE id = ?').get(req.session.userId) as User | undefined;
  if (!user || !bcrypt.compareSync(currentPassword, user.password_hash)) {
    res.status(401).json({ error: 'Current password is incorrect' });
    return;
  }

  const hash = bcrypt.hashSync(newPassword, 12);
  db.prepare('UPDATE users SET password_hash = ? WHERE id = ?').run(hash, req.session.userId);
  res.json({ message: 'Password updated' });
});

router.put('/currency', requireAuth, (req: AuthenticatedRequest, res: Response): void => {
  const { base_currency } = req.body;
  if (!base_currency) {
    res.status(400).json({ error: 'Base currency required' });
    return;
  }
  db.prepare('UPDATE users SET base_currency = ? WHERE id = ?').run(base_currency, req.session.userId);
  res.json({ message: 'Base currency updated' });
});

export default router;
