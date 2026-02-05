/**
 * Jest setup file to initialize sql.js before tests run
 */
const Database = require('./__mocks__/better-sqlite3');

module.exports = async () => {
  // Wait for sql.js to initialize
  await Database.initPromise();
  console.log('sql.js initialized successfully');
};
