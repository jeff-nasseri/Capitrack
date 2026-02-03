import Database from 'better-sqlite3';
import path from 'path';
import fs from 'fs';

// Database path configuration
let DB_PATH = process.env.DB_PATH || path.join(__dirname, '../../data/capitrack.db');

// Settings file path for persistent database path configuration
const SETTINGS_PATH = path.join(__dirname, '../../data/settings.json');

// Load settings if they exist
function loadSettings(): { db_path?: string } {
  try {
    if (fs.existsSync(SETTINGS_PATH)) {
      const content = fs.readFileSync(SETTINGS_PATH, 'utf-8');
      return JSON.parse(content);
    }
  } catch {
    // Ignore errors, use default
  }
  return {};
}

// Save settings
export function saveSettings(settings: { db_path?: string }): void {
  const dataDir = path.dirname(SETTINGS_PATH);
  if (!fs.existsSync(dataDir)) {
    fs.mkdirSync(dataDir, { recursive: true });
  }
  fs.writeFileSync(SETTINGS_PATH, JSON.stringify(settings, null, 2));
}

// Get current database path
export function getDatabasePath(): string {
  return DB_PATH;
}

// Set database path
export function setDatabasePath(newPath: string): void {
  DB_PATH = newPath;
  saveSettings({ db_path: newPath });
}

// Initialize settings from file
const savedSettings = loadSettings();
if (savedSettings.db_path) {
  DB_PATH = savedSettings.db_path;
}

// Ensure data directory exists
const dataDir = path.dirname(DB_PATH);
if (!fs.existsSync(dataDir)) {
  fs.mkdirSync(dataDir, { recursive: true });
}

// Create database instance
let db: Database.Database = new Database(DB_PATH);

// Initialize database schema
function initializeSchema(database: Database.Database): void {
  database.pragma('journal_mode = WAL');
  database.pragma('foreign_keys = ON');

  database.exec(`
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

    CREATE TABLE IF NOT EXISTS daily_wealth (
      date TEXT NOT NULL,
      total_wealth REAL NOT NULL DEFAULT 0,
      total_cost REAL NOT NULL DEFAULT 0,
      base_currency TEXT DEFAULT 'EUR',
      details TEXT DEFAULT '{}',
      updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
      PRIMARY KEY (date)
    );

    CREATE TABLE IF NOT EXISTS app_settings (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL,
      updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
    );

    CREATE INDEX IF NOT EXISTS idx_transactions_account ON transactions(account_id);
    CREATE INDEX IF NOT EXISTS idx_transactions_symbol ON transactions(symbol);
    CREATE INDEX IF NOT EXISTS idx_transactions_date ON transactions(date);
    CREATE INDEX IF NOT EXISTS idx_categories_parent ON categories(parent_id);
    CREATE INDEX IF NOT EXISTS idx_daily_wealth_date ON daily_wealth(date);
  `);

  // Schema migrations for backwards compatibility
  const migrations = [
    { table: 'goals', column: 'category_id', sql: 'ALTER TABLE goals ADD COLUMN category_id INTEGER DEFAULT NULL REFERENCES categories(id) ON DELETE SET NULL' },
    { table: 'goals', column: 'year', sql: 'ALTER TABLE goals ADD COLUMN year INTEGER DEFAULT NULL' },
    { table: 'goals', column: 'quarter', sql: 'ALTER TABLE goals ADD COLUMN quarter INTEGER DEFAULT NULL' },
    { table: 'goals', column: 'month', sql: 'ALTER TABLE goals ADD COLUMN month INTEGER DEFAULT NULL' },
    { table: 'goals', column: 'week', sql: 'ALTER TABLE goals ADD COLUMN week INTEGER DEFAULT NULL' },
    { table: 'goals', column: 'status', sql: "ALTER TABLE goals ADD COLUMN status TEXT DEFAULT 'not_started'" },
  ];

  for (const m of migrations) {
    const cols = database.pragma(`table_info(${m.table})`) as Array<{ name: string }>;
    if (!cols.map(c => c.name).includes(m.column)) {
      try {
        database.exec(m.sql);
      } catch (e) {
        console.warn(`Migration skipped (${m.table}.${m.column}):`, (e as Error).message);
      }
    }
  }

  // Create indexes that depend on migrated columns
  try {
    database.exec(`
      CREATE INDEX IF NOT EXISTS idx_goals_category ON goals(category_id);
      CREATE INDEX IF NOT EXISTS idx_goals_year_quarter ON goals(year, quarter);
    `);
  } catch (e) {
    console.warn('Index creation skipped:', (e as Error).message);
  }
}

// Initialize schema
initializeSchema(db);

// Function to reinitialize with a new database path
export function reinitializeDatabase(newPath: string): Database.Database {
  // Close existing connection
  db.close();

  // Update path
  DB_PATH = newPath;
  saveSettings({ db_path: newPath });

  // Ensure directory exists
  const newDataDir = path.dirname(newPath);
  if (!fs.existsSync(newDataDir)) {
    fs.mkdirSync(newDataDir, { recursive: true });
  }

  // Create new database instance
  db = new Database(newPath);
  initializeSchema(db);

  return db;
}

// Export database instance
export default db;
