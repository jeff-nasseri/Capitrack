import bcrypt from 'bcryptjs';
import db from './database';

// Check if users table is empty
const userCount = db.prepare('SELECT COUNT(*) as count FROM users').get() as { count: number };

if (userCount.count === 0) {
  console.log('Seeding database with initial data...');

  // Create default user
  const passwordHash = bcrypt.hashSync('Jeff123!Nasseri123!', 12);
  db.prepare(`
    INSERT INTO users (username, password_hash, base_currency)
    VALUES (?, ?, ?)
  `).run('JeffNasseri', passwordHash, 'EUR');

  // Create default accounts
  db.prepare(`
    INSERT INTO accounts (name, type, currency, description, icon, color)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run('Crypto Portfolio', 'crypto', 'USD', 'Main crypto holdings', 'bitcoin', '#f59e0b');

  db.prepare(`
    INSERT INTO accounts (name, type, currency, description, icon, color)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run('Stock Portfolio', 'stock', 'USD', 'US stock investments', 'chart-line', '#10b981');

  db.prepare(`
    INSERT INTO accounts (name, type, currency, description, icon, color)
    VALUES (?, ?, ?, ?, ?, ?)
  `).run('Commodities', 'commodity', 'EUR', 'Gold and precious metals', 'gem', '#8b5cf6');

  // Create default currency rates
  db.prepare(`
    INSERT OR REPLACE INTO currency_rates (from_currency, to_currency, rate)
    VALUES (?, ?, ?)
  `).run('USD', 'EUR', 0.92);

  db.prepare(`
    INSERT OR REPLACE INTO currency_rates (from_currency, to_currency, rate)
    VALUES (?, ?, ?)
  `).run('EUR', 'USD', 1.09);

  db.prepare(`
    INSERT OR REPLACE INTO currency_rates (from_currency, to_currency, rate)
    VALUES (?, ?, ?)
  `).run('GBP', 'EUR', 1.17);

  db.prepare(`
    INSERT OR REPLACE INTO currency_rates (from_currency, to_currency, rate)
    VALUES (?, ?, ?)
  `).run('EUR', 'GBP', 0.86);

  console.log('Database seeded successfully!');
}

export {};
