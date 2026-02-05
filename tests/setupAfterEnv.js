/**
 * Jest setup file that runs before each test file
 * Ensures sql.js is initialized
 */
const Database = require('./__mocks__/better-sqlite3');

beforeAll(async () => {
  await Database.initPromise();
});
