/**
 * Mock for better-sqlite3 that uses sql.js (pure JS SQLite)
 * This allows tests to run without native compilation
 */

let SQL = null;

// Synchronous initialization using require hook
// sql.js is loaded and initialized synchronously via the setup file
function getSQL() {
  if (SQL) return SQL;

  // Check if we have a cached version from global setup
  if (global.__SQL_JS__) {
    SQL = global.__SQL_JS__;
    return SQL;
  }

  // Fallback: try synchronous loading using sync-rpc pattern
  // This should work because sql.js WASM can be loaded sync in Node.js
  const initSqlJs = require('sql.js');

  // Create a simple synchronous wait
  let done = false;
  let error = null;

  initSqlJs().then(sql => {
    SQL = sql;
    global.__SQL_JS__ = sql;
    done = true;
  }).catch(err => {
    error = err;
    done = true;
  });

  // Spin wait - necessary for synchronous API compatibility
  const start = Date.now();
  while (!done && Date.now() - start < 10000) {
    // Use setImmediate equivalent via process._tickCallback if available
    if (typeof process._tickCallback === 'function') {
      process._tickCallback();
    }
  }

  if (error) throw error;
  if (!SQL) {
    throw new Error('sql.js failed to initialize within timeout');
  }

  return SQL;
}

class MockStatement {
  constructor(db, sql) {
    this.db = db;
    this.sql = sql;
  }

  run(...params) {
    try {
      this.db._db.run(this.sql, params.length > 0 ? params : undefined);
      return {
        changes: this.db._db.getRowsModified(),
        lastInsertRowid: this._getLastInsertRowId()
      };
    } catch (err) {
      throw new Error(`SQL Error: ${err.message}`);
    }
  }

  _getLastInsertRowId() {
    try {
      const result = this.db._db.exec('SELECT last_insert_rowid() as id');
      return result.length > 0 && result[0].values.length > 0
        ? result[0].values[0][0]
        : 0;
    } catch {
      return 0;
    }
  }

  get(...params) {
    try {
      const stmt = this.db._db.prepare(this.sql);
      if (params.length > 0) stmt.bind(params);
      if (stmt.step()) {
        const columns = stmt.getColumnNames();
        const values = stmt.get();
        const row = {};
        columns.forEach((col, i) => { row[col] = values[i]; });
        stmt.free();
        return row;
      }
      stmt.free();
      return undefined;
    } catch (err) {
      throw new Error(`SQL Error: ${err.message}`);
    }
  }

  all(...params) {
    try {
      const stmt = this.db._db.prepare(this.sql);
      if (params.length > 0) stmt.bind(params);
      const rows = [];
      while (stmt.step()) {
        const columns = stmt.getColumnNames();
        const values = stmt.get();
        const row = {};
        columns.forEach((col, i) => { row[col] = values[i]; });
        rows.push(row);
      }
      stmt.free();
      return rows;
    } catch (err) {
      throw new Error(`SQL Error: ${err.message}`);
    }
  }
}

class MockDatabase {
  constructor(filename) {
    const sql = getSQL();
    this._db = new sql.Database();
    this._filename = filename;
  }

  pragma(setting) {
    // Ignore pragmas for sql.js compatibility
    return undefined;
  }

  exec(sql) {
    try {
      this._db.run(sql);
    } catch (err) {
      throw new Error(`SQL Error: ${err.message}`);
    }
  }

  prepare(sql) {
    return new MockStatement(this, sql);
  }

  close() {
    if (this._db) {
      this._db.close();
      this._db = null;
    }
  }

  transaction(fn) {
    return (...args) => {
      this.exec('BEGIN TRANSACTION');
      try {
        const result = fn(...args);
        this.exec('COMMIT');
        return result;
      } catch (err) {
        this.exec('ROLLBACK');
        throw err;
      }
    };
  }
}

// Export initialization promise for setup file
MockDatabase.initPromise = async () => {
  const initSqlJs = require('sql.js');
  SQL = await initSqlJs();
  global.__SQL_JS__ = SQL;
  return SQL;
};

module.exports = MockDatabase;
