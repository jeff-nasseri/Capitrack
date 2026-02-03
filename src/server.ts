import express from 'express';
import path from 'path';
import session from 'express-session';
import SqliteStoreFactory from 'better-sqlite3-session-store';
import crypto from 'crypto';
import db from './db/database';
import { requireAuth } from './middleware/auth';
import authRoutes from './routes/auth';
import accountsRoutes from './routes/accounts';
import transactionsRoutes from './routes/transactions';
import goalsRoutes from './routes/goals';
import tagsRoutes from './routes/tags';
import currenciesRoutes from './routes/currencies';
import pricesRoutes from './routes/prices';
import settingsRoutes from './routes/settings';

const SqliteStore = SqliteStoreFactory(session);

const app = express();
const PORT = parseInt(process.env.PORT || '3000', 10);
const SESSION_SECRET = process.env.SESSION_SECRET || crypto.randomBytes(32).toString('hex');

// Middleware
app.use(express.json({ limit: '10mb' }));
app.use(session({
  store: new SqliteStore({
    client: db,
    expired: { clear: true, intervalMs: 900000 }
  }),
  secret: SESSION_SECRET,
  resave: false,
  saveUninitialized: false,
  cookie: {
    maxAge: 7 * 24 * 60 * 60 * 1000, // 7 days
    httpOnly: true,
    sameSite: 'strict'
  }
}));

// API routes
app.use('/api/auth', authRoutes);
app.use('/api/accounts', requireAuth, accountsRoutes);
app.use('/api/transactions', requireAuth, transactionsRoutes);
app.use('/api/goals', requireAuth, goalsRoutes);
app.use('/api/tags', requireAuth, tagsRoutes);
app.use('/api/currencies', requireAuth, currenciesRoutes);
app.use('/api/prices', requireAuth, pricesRoutes);
app.use('/api/settings', requireAuth, settingsRoutes);

// Static files
app.use(express.static(path.join(__dirname, 'public')));

// Serve static images
app.use('/static', express.static(path.join(__dirname, 'static')));

// SPA fallback
app.get('*', (_req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// Seed database on first run
import('./db/seed').catch(console.error);

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Capitrack running on port ${PORT}`);
});

export default app;
