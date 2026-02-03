import express, { Response } from 'express';
import path from 'path';
import fs from 'fs';
import { getDatabasePath, reinitializeDatabase } from '../db/database';
import { AuthenticatedRequest } from '../types';

const router = express.Router();

// Get current app settings
router.get('/', (_req: AuthenticatedRequest, res: Response): void => {
  res.json({
    db_path: getDatabasePath(),
    version: process.env.npm_package_version || '1.0.0',
    app_name: 'Capitrack',
    repository: 'https://github.com/jeff-nasseri/Capitrack',
    license: 'MIT'
  });
});

// Get database path
router.get('/database', (_req: AuthenticatedRequest, res: Response): void => {
  res.json({
    path: getDatabasePath(),
    exists: fs.existsSync(getDatabasePath())
  });
});

// Set database path and reinitialize
router.put('/database', (req: AuthenticatedRequest, res: Response): void => {
  const { path: newPath } = req.body;

  if (!newPath) {
    res.status(400).json({ error: 'Database path required' });
    return;
  }

  // Validate path is absolute or make it absolute
  const absolutePath = path.isAbsolute(newPath)
    ? newPath
    : path.resolve(process.cwd(), newPath);

  // Ensure the directory exists
  const dir = path.dirname(absolutePath);
  if (!fs.existsSync(dir)) {
    try {
      fs.mkdirSync(dir, { recursive: true });
    } catch (e) {
      res.status(400).json({ error: `Cannot create directory: ${(e as Error).message}` });
      return;
    }
  }

  try {
    // Reinitialize database with new path
    reinitializeDatabase(absolutePath);

    res.json({
      message: 'Database path updated successfully',
      path: absolutePath,
      exists: fs.existsSync(absolutePath)
    });
  } catch (e) {
    res.status(500).json({ error: `Failed to reinitialize database: ${(e as Error).message}` });
  }
});

// Refresh/reload the application state based on current database
router.post('/refresh', (_req: AuthenticatedRequest, res: Response): void => {
  try {
    const currentPath = getDatabasePath();

    // Reinitialize with current path to refresh schema
    reinitializeDatabase(currentPath);

    res.json({
      message: 'Application refreshed successfully',
      db_path: currentPath
    });
  } catch (e) {
    res.status(500).json({ error: `Failed to refresh: ${(e as Error).message}` });
  }
});

// Get about information
router.get('/about', (_req: AuthenticatedRequest, res: Response): void => {
  res.json({
    name: 'Capitrack',
    description: 'Personal wealth tracking and investment portfolio management platform',
    version: process.env.npm_package_version || '1.0.0',
    license: 'MIT',
    repository: 'https://github.com/jeff-nasseri/Capitrack',
    author: 'Jeff Nasseri',
    open_source: true
  });
});

export default router;
