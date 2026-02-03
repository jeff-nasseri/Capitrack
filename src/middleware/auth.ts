import { Response, NextFunction } from 'express';
import { AuthenticatedRequest } from '../types';

export function requireAuth(
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
): void {
  if (!req.session || !req.session.userId) {
    res.status(401).json({ error: 'Not authenticated' });
    return;
  }
  next();
}
