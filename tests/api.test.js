/**
 * Tests for API endpoints: accounts CRUD, transactions CRUD, goals (with hierarchy),
 * categories CRUD, tags CRUD, currencies, progress tracking.
 * Uses the actual Express app with an in-memory SQLite database.
 */

const Database = require('better-sqlite3');
const express = require('express');
const bcrypt = require('bcryptjs');
const session = require('express-session');

// Create the mock database at module level so jest.mock can reference it
const mockDb = new Database(':memory:');
mockDb.pragma('journal_mode = WAL');
mockDb.pragma('foreign_keys = ON');

mockDb.exec(`
  CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,
    base_currency TEXT DEFAULT 'EUR',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );
  CREATE TABLE IF NOT EXISTS accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    type TEXT NOT NULL DEFAULT 'general',
    currency TEXT DEFAULT 'EUR',
    description TEXT DEFAULT '',
    icon TEXT DEFAULT 'wallet',
    color TEXT DEFAULT '#6366f1',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );
  CREATE TABLE IF NOT EXISTS transactions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    account_id INTEGER NOT NULL,
    symbol TEXT NOT NULL,
    type TEXT NOT NULL CHECK(type IN ('buy','sell','transfer_in','transfer_out','dividend','interest','fee')),
    quantity REAL NOT NULL DEFAULT 0,
    price REAL NOT NULL DEFAULT 0,
    fee REAL DEFAULT 0,
    currency TEXT DEFAULT 'EUR',
    date TEXT NOT NULL,
    notes TEXT DEFAULT '',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE
  );
  CREATE TABLE IF NOT EXISTS categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    parent_id INTEGER DEFAULT NULL,
    color TEXT DEFAULT '#6366f1',
    icon TEXT DEFAULT 'folder',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (parent_id) REFERENCES categories(id) ON DELETE CASCADE
  );
  CREATE TABLE IF NOT EXISTS tags (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    color TEXT DEFAULT '#6366f1',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );
  CREATE TABLE IF NOT EXISTS goals (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    target_amount REAL NOT NULL DEFAULT 0,
    current_amount REAL DEFAULT 0,
    currency TEXT DEFAULT 'EUR',
    target_date TEXT NOT NULL,
    description TEXT DEFAULT '',
    achieved INTEGER DEFAULT 0,
    category_id INTEGER DEFAULT NULL,
    year INTEGER DEFAULT NULL,
    quarter INTEGER DEFAULT NULL,
    month INTEGER DEFAULT NULL,
    week INTEGER DEFAULT NULL,
    status TEXT DEFAULT 'not_started' CHECK(status IN ('not_started','in_progress','completed','on_hold','cancelled')),
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE SET NULL
  );
  CREATE TABLE IF NOT EXISTS goal_tags (
    goal_id INTEGER NOT NULL,
    tag_id INTEGER NOT NULL,
    PRIMARY KEY (goal_id, tag_id),
    FOREIGN KEY (goal_id) REFERENCES goals(id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
  );
  CREATE TABLE IF NOT EXISTS account_tags (
    account_id INTEGER NOT NULL,
    tag_id INTEGER NOT NULL,
    PRIMARY KEY (account_id, tag_id),
    FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
  );
  CREATE TABLE IF NOT EXISTS transaction_tags (
    transaction_id INTEGER NOT NULL,
    tag_id INTEGER NOT NULL,
    PRIMARY KEY (transaction_id, tag_id),
    FOREIGN KEY (transaction_id) REFERENCES transactions(id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
  );
  CREATE TABLE IF NOT EXISTS currency_rates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    from_currency TEXT NOT NULL,
    to_currency TEXT NOT NULL,
    rate REAL NOT NULL,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(from_currency, to_currency)
  );
  CREATE TABLE IF NOT EXISTS price_cache (
    symbol TEXT PRIMARY KEY,
    price REAL NOT NULL,
    currency TEXT DEFAULT 'USD',
    name TEXT DEFAULT '',
    change_percent REAL DEFAULT 0,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
  );
  CREATE INDEX IF NOT EXISTS idx_transactions_account ON transactions(account_id);
  CREATE INDEX IF NOT EXISTS idx_transactions_symbol ON transactions(symbol);
  CREATE INDEX IF NOT EXISTS idx_transactions_date ON transactions(date);
  CREATE INDEX IF NOT EXISTS idx_goals_category ON goals(category_id);
  CREATE INDEX IF NOT EXISTS idx_goals_year_quarter ON goals(year, quarter);
  CREATE INDEX IF NOT EXISTS idx_categories_parent ON categories(parent_id);
`);

const hash = bcrypt.hashSync('testpass', 4);
mockDb.prepare('INSERT INTO users (username, password_hash, base_currency) VALUES (?, ?, ?)').run('testuser', hash, 'EUR');

// Mock before requiring routes - mock both src and dist versions
jest.mock('../src/db/database', () => mockDb);
jest.mock('../dist/db/database', () => ({ default: mockDb, __esModule: true }));

let app;
let server;
let cookie;

beforeAll(async () => {
  app = express();
  app.use(express.json({ limit: '10mb' }));
  app.use(session({
    secret: 'test-secret',
    resave: false,
    saveUninitialized: false,
    cookie: { maxAge: 60000 }
  }));

  // Use compiled dist files and handle ES module default exports
  const authRoutes = require('../dist/routes/auth').default;
  const { requireAuth } = require('../dist/middleware/auth');
  const accountsRoutes = require('../dist/routes/accounts').default;
  const transactionsRoutes = require('../dist/routes/transactions').default;
  const goalsRoutes = require('../dist/routes/goals').default;
  const categoriesRoutes = require('../dist/routes/categories').default;
  const tagsRoutes = require('../dist/routes/tags').default;
  const currenciesRoutes = require('../dist/routes/currencies').default;

  app.use('/api/auth', authRoutes);
  app.use('/api/accounts', requireAuth, accountsRoutes);
  app.use('/api/transactions', requireAuth, transactionsRoutes);
  app.use('/api/goals', requireAuth, goalsRoutes);
  app.use('/api/categories', requireAuth, categoriesRoutes);
  app.use('/api/tags', requireAuth, tagsRoutes);
  app.use('/api/currencies', requireAuth, currenciesRoutes);

  await new Promise((resolve) => {
    server = app.listen(0, resolve);
  });

  // Login to get session cookie
  const port = server.address().port;
  const loginRes = await fetch(`http://localhost:${port}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'testuser', password: 'testpass' })
  });
  cookie = loginRes.headers.get('set-cookie');
});

afterAll(async () => {
  if (server) await new Promise(resolve => server.close(resolve));
  mockDb.close();
});

function getUrl(p) {
  return `http://localhost:${server.address().port}${p}`;
}

function authFetch(p, options = {}) {
  return fetch(getUrl(p), {
    ...options,
    headers: { 'Content-Type': 'application/json', Cookie: cookie, ...(options.headers || {}) }
  });
}

describe('Auth API', () => {
  test('POST /api/auth/login succeeds with correct credentials', async () => {
    const res = await fetch(getUrl('/api/auth/login'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'testuser', password: 'testpass' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.username).toBe('testuser');
  });

  test('POST /api/auth/login fails with wrong password', async () => {
    const res = await fetch(getUrl('/api/auth/login'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'testuser', password: 'wrong' })
    });
    expect(res.status).toBe(401);
  });

  test('GET /api/auth/session returns user when logged in', async () => {
    const res = await authFetch('/api/auth/session');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.username).toBe('testuser');
  });
});

describe('Accounts API', () => {
  let accountId;

  test('POST /api/accounts creates an account', async () => {
    const res = await authFetch('/api/accounts', {
      method: 'POST',
      body: JSON.stringify({ name: 'Test Stock', type: 'stock', currency: 'EUR', description: 'Test' })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.name).toBe('Test Stock');
    accountId = body.id;
  });

  test('GET /api/accounts lists all accounts', async () => {
    const res = await authFetch('/api/accounts');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
    expect(body.length).toBeGreaterThan(0);
  });

  test('GET /api/accounts/:id returns single account', async () => {
    const res = await authFetch(`/api/accounts/${accountId}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('Test Stock');
  });

  test('PUT /api/accounts/:id updates account', async () => {
    const res = await authFetch(`/api/accounts/${accountId}`, {
      method: 'PUT',
      body: JSON.stringify({ name: 'Updated Stock', type: 'stock', currency: 'USD' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('Updated Stock');
  });

  test('DELETE /api/accounts/:id deletes account', async () => {
    const res = await authFetch(`/api/accounts/${accountId}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
    const check = await authFetch(`/api/accounts/${accountId}`);
    expect(check.status).toBe(404);
  });
});

describe('Transactions API', () => {
  let accountId;
  let txId;

  beforeAll(async () => {
    const res = await authFetch('/api/accounts', {
      method: 'POST',
      body: JSON.stringify({ name: 'TX Test Account', type: 'stock', currency: 'EUR' })
    });
    const body = await res.json();
    accountId = body.id;
  });

  test('POST /api/transactions creates a transaction', async () => {
    const res = await authFetch('/api/transactions', {
      method: 'POST',
      body: JSON.stringify({
        account_id: accountId,
        symbol: 'AAPL',
        type: 'buy',
        quantity: 10,
        price: 185.5,
        fee: 1,
        currency: 'USD',
        date: '2024-01-15',
        notes: 'Test buy'
      })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.symbol).toBe('AAPL');
    txId = body.id;
  });

  test('GET /api/transactions lists transactions', async () => {
    const res = await authFetch(`/api/transactions?account_id=${accountId}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.length).toBeGreaterThan(0);
  });

  test('PUT /api/transactions/:id updates a transaction', async () => {
    const res = await authFetch(`/api/transactions/${txId}`, {
      method: 'PUT',
      body: JSON.stringify({
        account_id: accountId,
        symbol: 'AAPL',
        type: 'buy',
        quantity: 15,
        price: 190,
        fee: 0,
        currency: 'USD',
        date: '2024-01-15'
      })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.quantity).toBe(15);
  });

  test('DELETE /api/transactions/:id deletes a transaction', async () => {
    const res = await authFetch(`/api/transactions/${txId}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
  });
});

describe('Categories API', () => {
  let categoryId;
  let subCategoryId;

  test('POST /api/categories creates a category', async () => {
    const res = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name: 'Finance', color: '#22c55e', icon: 'briefcase' })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.name).toBe('Finance');
    expect(body.icon).toBe('briefcase');
    categoryId = body.id;
  });

  test('POST /api/categories creates a subcategory', async () => {
    const res = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name: 'Investments', parent_id: categoryId, color: '#6366f1' })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.parent_id).toBe(categoryId);
    subCategoryId = body.id;
  });

  test('POST /api/categories fails without name', async () => {
    const res = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ color: '#ff0000' })
    });
    expect(res.status).toBe(400);
  });

  test('GET /api/categories lists all categories', async () => {
    const res = await authFetch('/api/categories');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.length).toBeGreaterThanOrEqual(2);
  });

  test('GET /api/categories/tree returns hierarchy', async () => {
    const res = await authFetch('/api/categories/tree');
    expect(res.status).toBe(200);
    const body = await res.json();
    const finance = body.find(c => c.name === 'Finance');
    expect(finance).toBeDefined();
    expect(finance.children.length).toBeGreaterThan(0);
  });

  test('GET /api/categories/:id returns single category', async () => {
    const res = await authFetch(`/api/categories/${categoryId}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('Finance');
  });

  test('PUT /api/categories/:id updates category', async () => {
    const res = await authFetch(`/api/categories/${categoryId}`, {
      method: 'PUT',
      body: JSON.stringify({ name: 'Finance & Investing' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('Finance & Investing');
  });

  test('PUT /api/categories/:id prevents self-parent', async () => {
    const res = await authFetch(`/api/categories/${categoryId}`, {
      method: 'PUT',
      body: JSON.stringify({ parent_id: categoryId })
    });
    expect(res.status).toBe(400);
  });

  test('DELETE /api/categories/:id deletes category', async () => {
    // Delete subcategory first (or test cascade)
    const res = await authFetch(`/api/categories/${subCategoryId}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
  });
});

describe('Tags API', () => {
  let tagId;

  test('POST /api/tags creates a tag', async () => {
    const res = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'urgent', color: '#ef4444' })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.name).toBe('urgent');
    tagId = body.id;
  });

  test('POST /api/tags prevents duplicate names', async () => {
    const res = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'urgent' })
    });
    expect(res.status).toBe(400);
  });

  test('POST /api/tags fails without name', async () => {
    const res = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ color: '#ff0000' })
    });
    expect(res.status).toBe(400);
  });

  test('GET /api/tags lists all tags', async () => {
    const res = await authFetch('/api/tags');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.length).toBeGreaterThan(0);
  });

  test('GET /api/tags/:id returns single tag', async () => {
    const res = await authFetch(`/api/tags/${tagId}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('urgent');
  });

  test('PUT /api/tags/:id updates tag', async () => {
    const res = await authFetch(`/api/tags/${tagId}`, {
      method: 'PUT',
      body: JSON.stringify({ name: 'high-priority', color: '#f59e0b' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('high-priority');
  });

  test('DELETE /api/tags/:id deletes tag', async () => {
    const res = await authFetch(`/api/tags/${tagId}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
  });
});

describe('Goals API', () => {
  let goalId;
  let categoryId;
  let tagId1;
  let tagId2;

  beforeAll(async () => {
    // Create a category and tags for goal assignment
    let res = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name: 'Savings', color: '#22c55e' })
    });
    categoryId = (await res.json()).id;

    res = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'important', color: '#ef4444' })
    });
    tagId1 = (await res.json()).id;

    res = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'long-term', color: '#6366f1' })
    });
    tagId2 = (await res.json()).id;
  });

  test('POST /api/goals creates a goal with tags', async () => {
    const res = await authFetch('/api/goals', {
      method: 'POST',
      body: JSON.stringify({
        title: 'Emergency Fund',
        target_amount: 10000,
        target_date: '2026-12-31',
        description: 'Build emergency savings',
        category_id: categoryId,
        tag_ids: [tagId1, tagId2]
      })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.title).toBe('Emergency Fund');
    expect(body.category_id).toBe(categoryId);
    expect(body.tags).toHaveLength(2);
    goalId = body.id;
  });

  test('GET /api/goals lists goals with tags', async () => {
    const res = await authFetch('/api/goals');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.length).toBeGreaterThan(0);
    const goal = body.find(g => g.id === goalId);
    expect(goal.tags).toHaveLength(2);
  });

  test('GET /api/goals filters by category_id', async () => {
    const res = await authFetch(`/api/goals?category_id=${categoryId}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.length).toBeGreaterThan(0);
  });

  test('PUT /api/goals/:id updates goal with tags', async () => {
    const res = await authFetch(`/api/goals/${goalId}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: 'Emergency Fund Updated',
        target_amount: 15000,
        tag_ids: [tagId1]
      })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.title).toBe('Emergency Fund Updated');
    expect(body.target_amount).toBe(15000);
    expect(body.tags).toHaveLength(1);
  });

  test('POST /api/goals creates second goal', async () => {
    const res = await authFetch('/api/goals', {
      method: 'POST',
      body: JSON.stringify({
        title: 'Save for Vacation',
        target_amount: 3000,
        target_date: '2026-03-31'
      })
    });
    expect(res.status).toBe(201);
  });

  test('DELETE /api/goals/:id deletes a goal', async () => {
    const res = await authFetch(`/api/goals/${goalId}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
  });

  test('DELETE /api/goals deletes all goals', async () => {
    // Add a goal first
    await authFetch('/api/goals', {
      method: 'POST',
      body: JSON.stringify({ title: 'Temp', target_date: '2026-06-01' })
    });
    const res = await authFetch('/api/goals', { method: 'DELETE' });
    expect(res.status).toBe(200);
    const check = await authFetch('/api/goals');
    const body = await check.json();
    expect(body).toHaveLength(0);
  });
});

describe('Currency Rates API', () => {
  let rateId;

  test('POST /api/currencies creates a rate', async () => {
    const res = await authFetch('/api/currencies', {
      method: 'POST',
      body: JSON.stringify({ from_currency: 'GBP', to_currency: 'EUR', rate: 1.17 })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.from_currency).toBe('GBP');
    rateId = body.id;
  });

  test('GET /api/currencies lists rates', async () => {
    const res = await authFetch('/api/currencies');
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body)).toBe(true);
  });

  test('PUT /api/currencies/:id updates a rate', async () => {
    const res = await authFetch(`/api/currencies/${rateId}`, {
      method: 'PUT',
      body: JSON.stringify({ from_currency: 'GBP', to_currency: 'EUR', rate: 1.18 })
    });
    expect(res.status).toBe(200);
  });

  test('DELETE /api/currencies/:id deletes a rate', async () => {
    const res = await authFetch(`/api/currencies/${rateId}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
  });
});

describe('Auth Protection', () => {
  test('unauthenticated request to protected route returns 401', async () => {
    const res = await fetch(getUrl('/api/accounts'));
    expect(res.status).toBe(401);
  });

  test('unauthenticated request to categories returns 401', async () => {
    const res = await fetch(getUrl('/api/categories'));
    expect(res.status).toBe(401);
  });

  test('unauthenticated request to tags returns 401', async () => {
    const res = await fetch(getUrl('/api/tags'));
    expect(res.status).toBe(401);
  });
});

// ============================================================
// Comprehensive feature tests for all requested functionality
// ============================================================

describe('Categories and Subcategories (Settings)', () => {
  let parentId;
  let child1Id;
  let child2Id;

  test('creates top-level categories', async () => {
    const res1 = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name: 'Health', color: '#22c55e', icon: 'heart' })
    });
    expect(res1.status).toBe(201);
    parentId = (await res1.json()).id;

    const res2 = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name: 'Education', color: '#6366f1', icon: 'graduation-cap' })
    });
    expect(res2.status).toBe(201);
  });

  test('creates subcategories under a parent', async () => {
    const res1 = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name: 'Gym', parent_id: parentId, color: '#ef4444', icon: 'dumbbell' })
    });
    expect(res1.status).toBe(201);
    const body1 = await res1.json();
    expect(body1.parent_id).toBe(parentId);
    child1Id = body1.id;

    const res2 = await authFetch('/api/categories', {
      method: 'POST',
      body: JSON.stringify({ name: 'Nutrition', parent_id: parentId, color: '#f59e0b', icon: 'utensils' })
    });
    expect(res2.status).toBe(201);
    child2Id = (await res2.json()).id;
  });

  test('tree endpoint returns parent with children', async () => {
    const res = await authFetch('/api/categories/tree');
    expect(res.status).toBe(200);
    const body = await res.json();
    const health = body.find(c => c.name === 'Health');
    expect(health).toBeDefined();
    expect(health.children.length).toBeGreaterThanOrEqual(2);
    expect(health.children.map(c => c.name)).toEqual(expect.arrayContaining(['Gym', 'Nutrition']));
  });

  test('updates a subcategory', async () => {
    const res = await authFetch(`/api/categories/${child1Id}`, {
      method: 'PUT',
      body: JSON.stringify({ name: 'Gym & Fitness' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('Gym & Fitness');
  });

  test('deleting parent category', async () => {
    const res = await authFetch(`/api/categories/${parentId}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
  });
});

describe('Tags CRUD and Assignment to Goals', () => {
  let tag1Id, tag2Id, tag3Id, goalId;

  test('creates multiple tags', async () => {
    const res1 = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'personal', color: '#22c55e' })
    });
    expect(res1.status).toBe(201);
    tag1Id = (await res1.json()).id;

    const res2 = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'work', color: '#6366f1' })
    });
    expect(res2.status).toBe(201);
    tag2Id = (await res2.json()).id;

    const res3 = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'quarterly-review', color: '#f59e0b' })
    });
    expect(res3.status).toBe(201);
    tag3Id = (await res3.json()).id;
  });

  test('reads a specific tag', async () => {
    const res = await authFetch(`/api/tags/${tag1Id}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('personal');
    expect(body.color).toBe('#22c55e');
  });

  test('updates a tag', async () => {
    const res = await authFetch(`/api/tags/${tag2Id}`, {
      method: 'PUT',
      body: JSON.stringify({ name: 'business', color: '#4f46e5' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.name).toBe('business');
  });

  test('creates a goal with multiple tags assigned', async () => {
    const res = await authFetch('/api/goals', {
      method: 'POST',
      body: JSON.stringify({
        title: 'Tagged Goal',
        target_amount: 5000,
        target_date: '2026-06-30',
        tag_ids: [tag1Id, tag2Id, tag3Id]
      })
    });
    expect(res.status).toBe(201);
    const body = await res.json();
    expect(body.tags).toHaveLength(3);
    goalId = body.id;
  });

  test('updates goal tags (reassignment)', async () => {
    const res = await authFetch(`/api/goals/${goalId}`, {
      method: 'PUT',
      body: JSON.stringify({ tag_ids: [tag1Id] })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.tags).toHaveLength(1);
    expect(body.tags[0].name).toBe('personal');
  });

  test('deletes a tag', async () => {
    const res = await authFetch(`/api/tags/${tag3Id}`, { method: 'DELETE' });
    expect(res.status).toBe(200);
    const check = await authFetch(`/api/tags/${tag3Id}`);
    expect(check.status).toBe(404);
  });

  // cleanup
  afterAll(async () => {
    await authFetch(`/api/goals/${goalId}`, { method: 'DELETE' });
    await authFetch(`/api/tags/${tag1Id}`, { method: 'DELETE' });
    await authFetch(`/api/tags/${tag2Id}`, { method: 'DELETE' });
  });
});

describe('Goal Tag Filtering', () => {
  let tagId, goalId;

  beforeAll(async () => {
    // Clean slate
    await authFetch('/api/goals', { method: 'DELETE' });

    const tagRes = await authFetch('/api/tags', {
      method: 'POST',
      body: JSON.stringify({ name: 'filter-test-tag', color: '#ff0000' })
    });
    tagId = (await tagRes.json()).id;

    // Goal with tag
    const g1 = await authFetch('/api/goals', {
      method: 'POST',
      body: JSON.stringify({ title: 'With Tag', target_amount: 1000, target_date: '2026-12-31', tag_ids: [tagId] })
    });
    goalId = (await g1.json()).id;

    // Goal without tag
    await authFetch('/api/goals', {
      method: 'POST',
      body: JSON.stringify({ title: 'Without Tag', target_amount: 2000, target_date: '2026-12-31' })
    });
  });

  test('filters goals by tag_id', async () => {
    const res = await authFetch(`/api/goals?tag_id=${tagId}`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toHaveLength(1);
    expect(body[0].title).toBe('With Tag');
    expect(body[0].tags.some(t => t.id === tagId)).toBe(true);
  });

  afterAll(async () => {
    await authFetch('/api/goals', { method: 'DELETE' });
    await authFetch(`/api/tags/${tagId}`, { method: 'DELETE' });
  });
});
