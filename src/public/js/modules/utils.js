/* ===== Utility Functions Module ===== */

export function formatMoney(amount, currency = 'USD') {
  if (amount === undefined || amount === null) return '--';
  try { return new Intl.NumberFormat('en-US', { style: 'currency', currency, minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount); }
  catch { return `${currency} ${Number(amount).toFixed(2)}`; }
}

export function formatNumber(n) {
  if (n === undefined || n === null) return '--';
  if (Math.abs(n) < 0.01) return n.toFixed(8);
  if (Math.abs(n) < 1) return n.toFixed(4);
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 6 }).format(n);
}

export function formatCompact(n) {
  if (n === undefined || n === null || isNaN(n)) return '--';
  if (Math.abs(n) >= 1e12) return (n / 1e12).toFixed(1) + 'T';
  if (Math.abs(n) >= 1e9) return (n / 1e9).toFixed(1) + 'B';
  if (Math.abs(n) >= 1e6) return (n / 1e6).toFixed(1) + 'M';
  if (Math.abs(n) >= 1e3) return (n / 1e3).toFixed(1) + 'K';
  return n.toFixed(0);
}

export function formatDate(d) {
  if (!d) return '--';
  return new Date(d).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
}

export function esc(str) {
  if (!str) return '';
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}

export function toast(message, type = 'info') {
  const container = document.getElementById('toast-container');
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  const iconMap = { success: 'fa-check-circle', error: 'fa-exclamation-circle', info: 'fa-info-circle' };
  el.innerHTML = `<i class="fas ${iconMap[type] || iconMap.info}"></i> ${esc(message)}`;
  container.appendChild(el);
  setTimeout(() => { el.style.opacity = '0'; setTimeout(() => el.remove(), 300); }, 3000);
}

// Symbol icon: uses crypto SVG images from CDN, falls back to colored initials
const SYMBOL_COLORS = [
  '#6366f1', '#8b5cf6', '#a855f7', '#d946ef', '#ec4899',
  '#f43f5e', '#ef4444', '#f97316', '#f59e0b', '#eab308',
  '#84cc16', '#22c55e', '#14b8a6', '#06b6d4', '#0ea5e9',
  '#3b82f6', '#6366f1', '#8b5cf6'
];

export function getSymbolColor(symbol) {
  let hash = 0;
  for (let i = 0; i < symbol.length; i++) hash = symbol.charCodeAt(i) + ((hash << 5) - hash);
  return SYMBOL_COLORS[Math.abs(hash) % SYMBOL_COLORS.length];
}

export function getSymbolInitials(symbol) {
  // Remove common suffixes like -USD, =F
  const clean = symbol.replace(/-USD$/, '').replace(/=F$/, '');
  return clean.substring(0, 2).toUpperCase();
}

// Map of Yahoo Finance symbols to cryptocurrency-icons IDs (lowercase)
const CRYPTO_ICON_MAP = {
  'BTC-USD': 'btc', 'ETH-USD': 'eth', 'LTC-USD': 'ltc', 'XRP-USD': 'xrp',
  'ADA-USD': 'ada', 'DOT-USD': 'dot', 'DOGE-USD': 'doge', 'SOL-USD': 'sol',
  'AVAX-USD': 'avax', 'MATIC-USD': 'matic', 'LINK-USD': 'link', 'UNI-USD': 'uni',
  'ATOM-USD': 'atom', 'ALGO-USD': 'algo', 'XLM-USD': 'xlm', 'VET-USD': 'vet',
  'FIL-USD': 'fil', 'AAVE-USD': 'aave', 'MANA-USD': 'mana', 'SAND-USD': 'sand',
  'AXS-USD': 'axs', 'THETA-USD': 'theta', 'FTM-USD': 'ftm', 'NEAR-USD': 'near',
  'GRT-USD': 'grt', 'ENJ-USD': 'enj', 'BAT-USD': 'bat', 'CRV-USD': 'crv',
  'COMP-USD': 'comp', 'SNX-USD': 'snx', 'SUSHI-USD': 'sushi', 'YFI-USD': 'yfi',
  'MKR-USD': 'mkr', 'ZRX-USD': 'zrx', 'BNB-USD': 'bnb', 'TRX-USD': 'trx',
  'EOS-USD': 'eos', 'XTZ-USD': 'xtz', 'NEO-USD': 'neo', 'DASH-USD': 'dash',
  'ZEC-USD': 'zec', 'ETC-USD': 'etc', 'BCH-USD': 'bch', 'SHIB-USD': 'shib',
  'APE-USD': 'ape', 'CRO-USD': 'cro', 'LDO-USD': 'ldo', 'ARB-USD': 'arb',
  'OP-USD': 'op', 'SUI-USD': 'sui', 'PEPE-USD': 'pepe', 'IMX-USD': 'imx',
  'INJ-USD': 'inj', 'SEI-USD': 'sei', 'TIA-USD': 'tia', 'JUP-USD': 'jup',
  'RENDER-USD': 'rndr', 'FET-USD': 'fet', 'RUNE-USD': 'rune', 'STX-USD': 'stx',
};

// Map for commodity symbols to identifiable icons
const COMMODITY_ICON_MAP = {
  'GC=F': { name: 'Gold', icon: 'fa-coins', color: '#FFD700' },
  'SI=F': { name: 'Silver', icon: 'fa-coins', color: '#C0C0C0' },
  'PL=F': { name: 'Platinum', icon: 'fa-coins', color: '#E5E4E2' },
  'PA=F': { name: 'Palladium', icon: 'fa-coins', color: '#CED0DD' },
  'CL=F': { name: 'Oil', icon: 'fa-oil-can', color: '#333' },
  'NG=F': { name: 'Natural Gas', icon: 'fa-fire-flame-simple', color: '#FF6B35' },
};

// Image load cache to avoid re-requesting failed images
const _imageLoadCache = {};

/**
 * Get the CDN URL for a crypto symbol's SVG icon
 */
function getCryptoIconUrl(symbol) {
  const id = CRYPTO_ICON_MAP[symbol.toUpperCase()];
  if (!id) return null;
  // Using cryptocurrency-icons from jsdelivr CDN
  return `https://cdn.jsdelivr.net/gh/spothq/cryptocurrency-icons@master/svg/color/${id}.svg`;
}

export function getSymbolIcon(symbol) {
  const color = getSymbolColor(symbol);
  const initials = getSymbolInitials(symbol);
  const upperSymbol = symbol.toUpperCase();

  // Check for commodity
  const commodity = COMMODITY_ICON_MAP[upperSymbol];
  if (commodity) {
    return `<div class="symbol-icon" style="background:${commodity.color}"><i class="fas ${commodity.icon}" style="font-size:0.75rem;color:#fff;"></i></div>`;
  }

  // Check for crypto with known icon
  const iconUrl = getCryptoIconUrl(upperSymbol);
  if (iconUrl) {
    // Use img with fallback to initials
    return `<div class="symbol-icon symbol-icon-img" style="background:${color}" data-symbol="${esc(upperSymbol)}"><img src="${iconUrl}" alt="${initials}" onerror="this.style.display='none';this.parentElement.classList.remove('symbol-icon-img');this.parentElement.innerHTML='${initials}';"><span class="symbol-icon-fallback">${initials}</span></div>`;
  }

  return `<div class="symbol-icon" style="background:${color}">${initials}</div>`;
}

export function getSymbolIconSmall(symbol) {
  const color = getSymbolColor(symbol);
  const initials = getSymbolInitials(symbol);
  const upperSymbol = symbol.toUpperCase();

  // Check for commodity
  const commodity = COMMODITY_ICON_MAP[upperSymbol];
  if (commodity) {
    return `<div class="symbol-icon" style="background:${commodity.color};width:24px;height:24px;font-size:0.5rem;"><i class="fas ${commodity.icon}" style="font-size:0.5rem;color:#fff;"></i></div>`;
  }

  // Check for crypto with known icon
  const iconUrl = getCryptoIconUrl(upperSymbol);
  if (iconUrl) {
    return `<div class="symbol-icon symbol-icon-img" style="background:${color};width:24px;height:24px;font-size:0.5rem;" data-symbol="${esc(upperSymbol)}"><img src="${iconUrl}" alt="${initials}" onerror="this.style.display='none';this.parentElement.classList.remove('symbol-icon-img');this.parentElement.innerHTML='${initials}';"><span class="symbol-icon-fallback">${initials}</span></div>`;
  }

  return `<div class="symbol-icon" style="background:${color};width:24px;height:24px;font-size:0.5rem;">${initials}</div>`;
}
