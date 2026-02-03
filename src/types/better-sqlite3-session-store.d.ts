declare module 'better-sqlite3-session-store' {
  import session from 'express-session';
  import Database from 'better-sqlite3';

  interface SqliteStoreOptions {
    client: Database.Database;
    expired?: {
      clear?: boolean;
      intervalMs?: number;
    };
  }

  function SqliteStoreFactory(session: typeof import('express-session')): {
    new (options: SqliteStoreOptions): session.Store;
  };

  export = SqliteStoreFactory;
}
