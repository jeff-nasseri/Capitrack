/* ===== Application State Module ===== */

export const state = {
  user: null,
  accounts: [],
  currentAccountId: null,
  currentSymbol: null,
  dashboardChart: null,
  accountChart: null,
  symbolChart: null,
  allHoldings: [],
  dashboardSummary: null,
  settings: {
    showTransactionDots: JSON.parse(localStorage.getItem('wealth-show-transaction-dots') ?? 'true')
  }
};

export function updateSetting(key, value) {
  state.settings[key] = value;
  localStorage.setItem(`wealth-${key.replace(/([A-Z])/g, '-$1').toLowerCase()}`, JSON.stringify(value));
}

export function getSetting(key) {
  return state.settings[key];
}
