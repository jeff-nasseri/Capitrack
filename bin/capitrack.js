#!/usr/bin/env node

const path = require("path");
const fs = require("fs");

// Set default titles/info
process.title = "capitrack";

// Ensure we are running from the dist directory or can find it
const serverPath = path.join(__dirname, "../dist/server.js");

if (!fs.existsSync(serverPath)) {
  console.error(
    'Error: Capitrack server not found. Please run "npm run build" first.',
  );
  process.exit(1);
}

// Check for DB_PATH, if not set, suggest or set a default in user's home directory
if (!process.env.DB_PATH) {
  const homeDir =
    process.env.HOME || process.env.USERPROFILE || process.env.HOMEPATH;
  if (homeDir) {
    const defaultDbDir = path.join(homeDir, ".capitrack");
    if (!fs.existsSync(defaultDbDir)) {
      fs.mkdirSync(defaultDbDir, { recursive: true });
    }
    process.env.DB_PATH = path.join(defaultDbDir, "capitrack.db");
    console.log(`Using default database at: ${process.env.DB_PATH}`);
  }
}

// Start the server
require(serverPath);
