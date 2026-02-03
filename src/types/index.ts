import { Request } from 'express';
import { Session, SessionData } from 'express-session';

// Extend express-session
declare module 'express-session' {
  interface SessionData {
    userId: number;
    username: string;
  }
}

// Database models
export interface User {
  id: number;
  username: string;
  password_hash: string;
  base_currency: string;
  created_at: string;
}

export interface Account {
  id: number;
  name: string;
  type: string;
  currency: string;
  description: string;
  icon: string;
  color: string;
  created_at: string;
  updated_at: string;
  tags?: Tag[];
}

export interface Transaction {
  id: number;
  account_id: number;
  symbol: string;
  type: 'buy' | 'sell' | 'transfer_in' | 'transfer_out' | 'dividend' | 'interest' | 'fee';
  quantity: number;
  price: number;
  fee: number;
  currency: string;
  date: string;
  notes: string;
  created_at: string;
}

export interface Category {
  id: number;
  name: string;
  parent_id: number | null;
  color: string;
  icon: string;
  created_at: string;
  children?: Category[];
}

export interface Tag {
  id: number;
  name: string;
  color: string;
  created_at: string;
}

export interface Goal {
  id: number;
  title: string;
  target_amount: number;
  current_amount: number;
  currency: string;
  target_date: string;
  description: string;
  achieved: number;
  category_id: number | null;
  year: number | null;
  quarter: number | null;
  month: number | null;
  week: number | null;
  status: 'not_started' | 'in_progress' | 'completed' | 'on_hold' | 'cancelled';
  created_at: string;
  updated_at: string;
  tags?: Tag[];
}

export interface CurrencyRate {
  id: number;
  from_currency: string;
  to_currency: string;
  rate: number;
  updated_at: string;
}

export interface PriceCache {
  symbol: string;
  price: number;
  currency: string;
  name: string;
  change_percent: number;
  updated_at: string;
}

export interface DailyWealth {
  date: string;
  total_wealth: number;
  total_cost: number;
  base_currency: string;
  details: string;
  updated_at: string;
}

// API request types
export interface AuthenticatedRequest extends Request {
  session: Session & Partial<SessionData> & {
    userId?: number;
    username?: string;
  };
}

// Holdings calculation types
export interface Holding {
  symbol: string;
  quantity: number;
  avg_cost: number;
  total_cost: number;
  transactions: number;
  first_date: string;
  last_date: string;
}

export interface AccountHolding extends Holding {
  account_id: number;
  account_currency: string;
}

export interface EnrichedHolding extends AccountHolding {
  price: number;
  name: string;
  change_percent: number;
  market_value: number;
  cost_basis: number;
  gain: number;
  gain_pct: number;
  currency: string;
}

// Dashboard summary types
export interface AccountSummary {
  account_id: number;
  account_name: string;
  currency: string;
  market_value: number;
  cost_basis: number;
  gain: number;
  gain_percent: number;
}

export interface DashboardSummary {
  total_wealth: number;
  total_cost: number;
  total_gain: number;
  total_gain_percent: number;
  base_currency: string;
  accounts: AccountSummary[];
}

// Goal progress types
export interface WeekProgress {
  week: number;
  total: number;
  completed: number;
  progress: number;
}

export interface MonthProgress {
  month: number;
  total: number;
  completed: number;
  progress: number;
  weeks: WeekProgress[];
}

export interface QuarterProgress {
  quarter: number;
  total: number;
  completed: number;
  progress: number;
  months: MonthProgress[];
}

export interface YearProgress {
  year: number;
  total: number;
  completed: number;
  progress: number;
  quarters: QuarterProgress[];
}

// CSV import types
export type CSVFormat = 'revolut-stocks' | 'revolut-commodities' | 'trezor' | 'generic' | 'unknown';

export interface ImportedTransaction {
  symbol: string;
  type: Transaction['type'];
  quantity: number;
  price: number;
  fee: number;
  currency: string;
  date: string;
  notes: string;
}

export interface ImportResult {
  imported: number;
  skipped: number;
  errors: string[];
}

// Price quote types
export interface PriceQuote {
  symbol: string;
  price: number;
  currency: string;
  name: string;
  change_percent: number;
  regularMarketTime?: Date;
}

export interface HistoricalPrice {
  date: string;
  close: number;
  open?: number;
  high?: number;
  low?: number;
  volume?: number;
}

// Settings types
export interface AppSettings {
  db_path: string;
}

// API response types
export interface ApiError {
  error: string;
}

export interface ApiSuccess {
  message: string;
}
