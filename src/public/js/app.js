/* ===== Wealth Tracker - Main Entry Point ===== */
/* Clean architecture: modules/api, modules/state, modules/utils, modules/theme, modules/modal
   components/chart, components/holdings, components/transactions, components/forms
   pages/dashboard, pages/account, pages/symbol, pages/settings */

import { API } from './modules/api.js';
import { state } from './modules/state.js';
import { formatMoney, formatNumber, formatDate, esc, toast, getSymbolIcon } from './modules/utils.js';
import { initTheme, setTheme, updateThemeButtons } from './modules/theme.js';
import { openModal, closeModal, openImportModal, closeImportModal, initModalListeners } from './modules/modal.js';
import { renderLineChart } from './components/chart.js';
import { renderHoldingsTable } from './components/holdings.js';
import { renderActivityTable, transactionFormHtml, getTransactionFormData } from './components/transactions.js';
import { initSymbolSearch } from './components/forms.js';
import { loadDashboard, loadDashboardChart } from './pages/dashboard.js';
import { loadAccountDetail, loadAccountChart } from './pages/account.js';
import { loadSymbolDetail, loadSymbolChart } from './pages/symbol.js';
import { loadSettings, showAddAccountModal, showEditAccountModal, deleteAccount, showAddGoalModal, showEditGoalModal, deleteGoal, removeAllGoals, showAddTagModal, showEditTagModal, deleteTag, showAddRateModal, showEditRateModal, deleteRate } from './pages/settings.js';
import { loadCalendar, calendarPrev, calendarNext, calendarToday, setCalendarView } from './pages/calendar.js';

// ===== Init =====
document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  initSidebar();
  initEventListeners();
  initModalListeners();
  await checkSession();
});

// Listen for auth unauthorized events
window.addEventListener('auth:unauthorized', () => showPage('login'));

// Listen for theme changes to redraw charts
window.addEventListener('theme:changed', () => {
  if (state.dashboardChart) loadDashboardChart();
  if (state.accountChart) loadAccountChart(state.currentAccountId);
  if (state.symbolChart) loadSymbolChart(state.currentSymbol);
});

// Listen for accounts changed (e.g., after creating/deleting account)
window.addEventListener('accounts:changed', () => {
  // Refresh whichever page is currently active
  const accountsListPage = document.getElementById('accounts-list-page');
  const dashboardPage = document.getElementById('dashboard-page');
  if (accountsListPage && accountsListPage.classList.contains('active')) loadAccountsList();
  if (dashboardPage && dashboardPage.classList.contains('active')) loadDashboard();
});

// Listen for currency changed (refresh dashboard to show new currency)
window.addEventListener('currency:changed', () => {
  const dashboardPage = document.getElementById('dashboard-page');
  if (dashboardPage && dashboardPage.classList.contains('active')) loadDashboard();
});

async function checkSession() {
  try {
    const res = await fetch('/api/auth/session');
    if (res.ok) { state.user = await res.json(); showApp(); }
    else showPage('login');
  } catch { showPage('login'); }
}

// ===== Event Listeners =====
function initEventListeners() {
  // Login
  document.getElementById('login-form').addEventListener('submit', handleLogin);
  document.getElementById('logout-btn').addEventListener('click', handleLogout);

  // Sidebar toggle
  document.getElementById('sidebar-toggle').addEventListener('click', toggleSidebar);

  // Navigation
  document.querySelectorAll('.nav-item[data-page]').forEach(item => {
    item.addEventListener('click', () => navigateTo(item.dataset.page));
  });

  // Sidebar logo click - navigate to dashboard
  document.querySelector('.sidebar-logo[data-page]')?.addEventListener('click', (e) => {
    e.preventDefault();
    navigateTo('dashboard');
  });

  // Dashboard
  document.getElementById('refresh-wealth-btn').addEventListener('click', refreshDashboard);
  document.getElementById('accounts-list-btn')?.addEventListener('click', () => navigateTo('accounts-list'));
  document.getElementById('view-all-holdings')?.addEventListener('click', (e) => { e.preventDefault(); navigateTo('holdings'); });

  // Dashboard chart periods
  document.querySelectorAll('#dashboard-periods .period-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#dashboard-periods .period-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      loadDashboardChart(btn.dataset.period);
    });
  });

  // Account detail
  document.getElementById('back-to-dashboard').addEventListener('click', () => navigateTo('dashboard'));
  document.getElementById('add-tx-btn').addEventListener('click', showAddTransactionModal);
  document.getElementById('import-tx-btn').addEventListener('click', openImportModal);
  document.getElementById('export-tx-btn').addEventListener('click', exportTransactions);
  document.querySelector('.refresh-account').addEventListener('click', () => loadAccountDetail(state.currentAccountId));

  // Account chart periods
  document.querySelectorAll('#account-periods .period-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#account-periods .period-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      loadAccountChart(state.currentAccountId, btn.dataset.period);
    });
  });

  // Symbol detail
  document.getElementById('back-to-account').addEventListener('click', () => showAccountPage(state.currentAccountId));

  // Symbol chart periods
  document.querySelectorAll('#symbol-periods .period-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('#symbol-periods .period-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      loadSymbolChart(state.currentSymbol, btn.dataset.period);
    });
  });

  // Symbol tabs
  document.querySelectorAll('.symbol-tab').forEach(tab => {
    tab.addEventListener('click', () => {
      document.querySelectorAll('.symbol-tab').forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      document.querySelectorAll('.symbol-tab-content').forEach(c => c.classList.remove('active'));
      document.getElementById(`symbol-tab-${tab.dataset.tab}`)?.classList.add('active');
    });
  });

  // Activity export
  document.getElementById('activity-export-btn')?.addEventListener('click', () => {
    window.location.href = '/api/transactions/export/csv';
  });

  // Calendar controls
  document.getElementById('cal-prev').addEventListener('click', calendarPrev);
  document.getElementById('cal-next').addEventListener('click', calendarNext);
  document.getElementById('cal-today').addEventListener('click', calendarToday);
  document.querySelectorAll('.cal-view-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.cal-view-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      setCalendarView(btn.dataset.view);
    });
  });

  // Settings submenu
  document.querySelectorAll('.settings-menu-item').forEach(item => {
    item.addEventListener('click', () => {
      document.querySelectorAll('.settings-menu-item').forEach(i => i.classList.remove('active'));
      item.classList.add('active');
      document.querySelectorAll('.settings-panel').forEach(p => p.classList.remove('active'));
      document.getElementById(`settings-${item.dataset.settings}`)?.classList.add('active');
    });
  });

  // Accounts list add button
  document.getElementById('accounts-add-btn').addEventListener('click', showAddAccountModal);

  // Settings actions
  document.getElementById('add-account-btn').addEventListener('click', showAddAccountModal);
  document.getElementById('add-goal-btn').addEventListener('click', showAddGoalModal);
  document.getElementById('settings-add-goal-btn').addEventListener('click', showAddGoalModal);
  document.getElementById('remove-all-goals-btn').addEventListener('click', removeAllGoals);
  document.getElementById('add-tag-btn').addEventListener('click', showAddTagModal);
  document.getElementById('add-rate-btn').addEventListener('click', showAddRateModal);
  document.getElementById('password-form').addEventListener('submit', handlePasswordChange);
  document.getElementById('purge-btn').addEventListener('click', handlePurge);

  // Goals year navigation
  document.getElementById('goals-year-prev').addEventListener('click', () => { goalsYear--; document.getElementById('goals-year-label').textContent = goalsYear; loadGoals(); });
  document.getElementById('goals-year-next').addEventListener('click', () => { goalsYear++; document.getElementById('goals-year-label').textContent = goalsYear; loadGoals(); });

  // Theme buttons
  document.querySelectorAll('.theme-option').forEach(btn => {
    btn.addEventListener('click', () => setTheme(btn.dataset.themeVal));
  });

  // Import
  document.getElementById('import-form').addEventListener('submit', handleImport);
  document.getElementById('import-file').addEventListener('change', handleFileSelected);
}

// ===== Auth =====
async function handleLogin(e) {
  e.preventDefault();
  const username = document.getElementById('login-username').value;
  const password = document.getElementById('login-password').value;
  const errorEl = document.getElementById('login-error');
  try {
    const res = await fetch('/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password }) });
    if (res.ok) { state.user = await res.json(); errorEl.classList.add('hidden'); showApp(); }
    else { const data = await res.json(); errorEl.textContent = data.error || 'Login failed'; errorEl.classList.remove('hidden'); }
  } catch { errorEl.textContent = 'Connection error'; errorEl.classList.remove('hidden'); }
}

async function handleLogout() {
  await fetch('/api/auth/logout', { method: 'POST' });
  state.user = null;
  showPage('login');
}

// ===== Navigation =====
function showPage(page) {
  if (page === 'login') {
    document.getElementById('login-page').classList.remove('hidden');
    document.getElementById('login-page').classList.add('active');
    document.getElementById('main-app').classList.add('hidden');
  }
}

function showApp() {
  document.getElementById('login-page').classList.add('hidden');
  document.getElementById('login-page').classList.remove('active');
  document.getElementById('main-app').classList.remove('hidden');

  // Show loading overlay until dashboard is loaded
  showDashboardLoading(true);
  navigateTo('dashboard');
  initDragAndDrop();
}

function showDashboardLoading(show) {
  let overlay = document.getElementById('dashboard-loading');
  if (show) {
    if (!overlay) {
      overlay = document.createElement('div');
      overlay.id = 'dashboard-loading';
      overlay.className = 'dashboard-loading-overlay';
      overlay.innerHTML = '<div class="loading-spinner">Loading your portfolio...</div>';
      document.getElementById('main-app').appendChild(overlay);
    }
    overlay.style.display = 'flex';
  } else {
    if (overlay) overlay.style.display = 'none';
  }
}

function navigateTo(page) {
  document.querySelectorAll('.nav-item').forEach(item => item.classList.toggle('active', item.dataset.page === page));
  document.querySelectorAll('#content > .page').forEach(p => { p.classList.remove('active'); p.classList.add('hidden'); });
  const pageEl = document.getElementById(`${page}-page`);
  if (pageEl) { pageEl.classList.remove('hidden'); pageEl.classList.add('active'); }

  switch (page) {
    case 'dashboard': loadDashboard(); break;
    case 'holdings': loadAllHoldings(); break;
    case 'accounts-list': loadAccountsList(); break;
    case 'activity': loadActivity(); break;
    case 'calendar': loadCalendar(); break;
    case 'goals': loadGoals(); break;
    case 'settings': loadSettings(); break;
  }
}

// ===== Account Page =====
async function showAccountPage(accountId) {
  state.currentAccountId = accountId;
  document.querySelectorAll('#content > .page').forEach(p => { p.classList.remove('active'); p.classList.add('hidden'); });
  document.getElementById('account-page').classList.remove('hidden');
  document.getElementById('account-page').classList.add('active');
  await loadAccountDetail(accountId);
}

// ===== Symbol Page =====
async function showSymbolPage(accountId, symbol) {
  state.currentAccountId = accountId;
  state.currentSymbol = symbol;
  document.querySelectorAll('#content > .page').forEach(p => { p.classList.remove('active'); p.classList.add('hidden'); });
  document.getElementById('symbol-page').classList.remove('hidden');
  document.getElementById('symbol-page').classList.add('active');
  document.querySelectorAll('.symbol-tab').forEach(t => t.classList.toggle('active', t.dataset.tab === 'overview'));
  document.querySelectorAll('.symbol-tab-content').forEach(c => c.classList.toggle('active', c.id === 'symbol-tab-overview'));
  await loadSymbolDetail(accountId, symbol);
}

// ===== All Holdings Page =====
async function loadAllHoldings() {
  const container = document.getElementById('all-holdings-list');
  container.innerHTML = '<div class="loading-spinner">Loading holdings...</div>';
  try {
    if (!state.allHoldings.length) {
      const accounts = state.accounts.length ? state.accounts : await API.get('/api/accounts') || [];
      state.accounts = accounts;
      let allHoldings = [];
      for (const account of accounts) {
        const holdings = await API.get(`/api/accounts/${account.id}/holdings`);
        if (holdings) for (const h of holdings) allHoldings.push({ ...h, account_id: account.id, account_currency: account.currency });
      }
      const symbols = [...new Set(allHoldings.map(h => h.symbol))];
      let prices = {};
      if (symbols.length) prices = await API.post('/api/prices/quotes', { symbols }) || {};
      state.allHoldings = allHoldings.map(h => {
        const p = prices[h.symbol] || {};
        const marketValue = h.quantity * (p.price || 0);
        const costBasis = h.quantity * (h.avg_cost || 0);
        return { ...h, price: p.price || 0, name: p.name || h.symbol, change_percent: p.change_percent || 0, market_value: marketValue, cost_basis: costBasis, gain: marketValue - costBasis, gain_pct: costBasis > 0 ? ((marketValue - costBasis) / costBasis) * 100 : 0, currency: p.currency || 'USD' };
      }).sort((a, b) => b.market_value - a.market_value);
    }
    renderHoldingsTable(state.allHoldings, container);
  } catch (e) { console.error('All holdings error:', e); container.innerHTML = '<div class="empty-state"><p>Failed to load holdings.</p></div>'; }
}

// ===== Accounts List Page =====
async function loadAccountsList() {
  const grid = document.getElementById('accounts-grid');
  try {
    const [accounts, summary] = await Promise.all([API.get('/api/accounts'), API.get('/api/prices/dashboard/summary')]);
    state.accounts = accounts || [];
    const accountValueMap = {};
    for (const sa of (summary?.accounts || [])) accountValueMap[sa.account_id] = sa;

    grid.innerHTML = accounts.map(a => {
      const sa = accountValueMap[a.id] || {};
      const value = sa.market_value || 0;
      const holdings = sa.holdings_count || 0;
      const iconMap = { bitcoin: 'fa-bitcoin-sign', 'chart-line': 'fa-chart-line', gem: 'fa-gem', wallet: 'fa-wallet', bank: 'fa-building-columns', piggy: 'fa-piggy-bank' };
      const iconClass = iconMap[a.icon] || 'fa-wallet';
      return `<div class="account-card" onclick="showAccountPage(${a.id})">
        <div class="account-card-header">
          <div class="account-icon" style="background:${a.color}"><i class="fas ${iconClass}"></i></div>
          <h4>${esc(a.name)}</h4>
          <span class="account-type-badge">${esc(a.type)}</span>
        </div>
        <div class="account-card-value">${formatMoney(value, a.currency)}</div>
        <div class="account-card-info"><span>${holdings} holding${holdings !== 1 ? 's' : ''}</span><span>${a.currency}</span></div>
      </div>`;
    }).join('');
  } catch (e) { console.error('Accounts list error:', e); }
}

// ===== Activity Page =====
async function loadActivity() {
  try {
    const transactions = await API.get('/api/transactions?limit=200');
    renderActivityTable(transactions, document.getElementById('activity-list'));
  } catch (e) { console.error('Activity error:', e); }
}

// ===== Goals Page =====
let goalsYear = new Date().getFullYear();

async function loadGoals() {
  const monthNames = ['', 'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  const statusIcons = { not_started: 'fa-circle text-muted', in_progress: 'fa-spinner text-primary', completed: 'fa-check-circle text-success', on_hold: 'fa-pause-circle text-warning', cancelled: 'fa-times-circle text-danger' };

  document.getElementById('goals-year-label').textContent = goalsYear;

  try {
    const [progress, goals] = await Promise.all([
      API.get(`/api/goals/progress?year=${goalsYear}`),
      API.get(`/api/goals?year=${goalsYear}`)
    ]);

    // Year progress bar
    const yearProgressEl = document.getElementById('goals-year-progress');
    yearProgressEl.innerHTML = `
      <div class="year-progress-card">
        <div class="year-progress-header">
          <h3>${goalsYear} Progress</h3>
          <span class="year-progress-stats">${progress.completed}/${progress.total} completed</span>
        </div>
        <div class="goal-progress-bar year-bar"><div class="goal-progress-fill" style="width:${progress.progress}%"></div></div>
        <div class="goal-progress-text"><span>${progress.progress}% complete</span></div>
      </div>`;

    // Hierarchy
    const hierarchyEl = document.getElementById('goals-hierarchy');
    if (!progress.quarters.some(q => q.total > 0) && !goals?.length) {
      hierarchyEl.innerHTML = `<div class="empty-state"><i class="fas fa-bullseye"></i><p>No goals for ${goalsYear}.</p></div>`;
      return;
    }

    let html = '';
    for (const q of progress.quarters) {
      const qGoals = (goals || []).filter(g => g.quarter === q.quarter && !g.month);
      html += `<div class="quarter-section">
        <div class="quarter-header" onclick="this.parentElement.classList.toggle('collapsed')">
          <div class="quarter-title"><i class="fas fa-chevron-down quarter-chevron"></i><h3>Q${q.quarter}</h3></div>
          <div class="quarter-progress-info">
            <span>${q.completed}/${q.total}</span>
            <div class="mini-progress-bar"><div class="mini-progress-fill" style="width:${q.progress}%"></div></div>
            <span>${q.progress}%</span>
          </div>
        </div>`;

      // Quarter-level goals (no month)
      if (qGoals.length) {
        html += `<div class="quarter-goals">${qGoals.map(g => renderGoalCard(g, statusIcons)).join('')}</div>`;
      }

      // Months
      for (const m of q.months) {
        const mGoals = (goals || []).filter(g => g.quarter === q.quarter && g.month === m.month && !g.week);

        html += `<div class="month-section">
          <div class="month-header" onclick="this.parentElement.classList.toggle('collapsed')">
            <div class="month-title"><i class="fas fa-chevron-down month-chevron"></i><h4>${monthNames[m.month]}</h4></div>
            <div class="quarter-progress-info">
              <span>${m.completed}/${m.total}</span>
              <div class="mini-progress-bar"><div class="mini-progress-fill" style="width:${m.progress}%"></div></div>
              <span>${m.progress}%</span>
            </div>
          </div>`;

        // Month-level goals (no week)
        if (mGoals.length) {
          html += `<div class="month-goals">${mGoals.map(g => renderGoalCard(g, statusIcons)).join('')}</div>`;
        }

        // Weeks
        for (const w of m.weeks) {
          if (w.total === 0) continue;
          const wGoals = (goals || []).filter(g => g.quarter === q.quarter && g.month === m.month && g.week === w.week);

          html += `<div class="week-section">
            <div class="week-header">
              <h5>Week ${w.week}</h5>
              <div class="quarter-progress-info">
                <span>${w.completed}/${w.total}</span>
                <div class="mini-progress-bar"><div class="mini-progress-fill" style="width:${w.progress}%"></div></div>
                <span>${w.progress}%</span>
              </div>
            </div>
            <div class="week-goals">${wGoals.map(g => renderGoalCard(g, statusIcons)).join('')}</div>
          </div>`;
        }

        html += '</div>'; // close month-section
      }

      html += '</div>'; // close quarter-section
    }

    hierarchyEl.innerHTML = html;
  } catch (e) { console.error('Goals error:', e); }
}

function renderGoalCard(g, statusIcons) {
  const statusIcon = statusIcons[g.status] || statusIcons.not_started;
  const tagBadges = (g.tags || []).map(t => `<span class="tag-badge" style="background:${t.color}20;color:${t.color}">${esc(t.name)}</span>`).join('');
  return `<div class="goal-card-compact ${g.status === 'completed' || g.achieved ? 'achieved' : ''}">
    <div class="goal-card-compact-header">
      <i class="fas ${statusIcon}"></i>
      <span class="goal-card-title">${esc(g.title)}</span>
      <span class="goal-card-amount">${formatMoney(g.current_amount, g.currency)} / ${formatMoney(g.target_amount, g.currency)}</span>
    </div>
    ${tagBadges ? `<div class="goal-card-tags">${tagBadges}</div>` : ''}
  </div>`;
}

// ===== Dashboard refresh =====
async function refreshDashboard() {
  const btn = document.getElementById('refresh-wealth-btn');
  btn.querySelector('i').classList.add('spinning');
  try {
    await API.post('/api/prices/quotes', { symbols: [] });
    await loadDashboard();
    toast('Prices refreshed', 'success');
  } catch { toast('Failed to refresh', 'error'); }
  btn.querySelector('i').classList.remove('spinning');
}

// ===== Transaction Modals =====
async function showAddTransactionModal() {
  const tags = await API.get('/api/tags');
  openModal('Add Transaction', transactionFormHtml({}, tags || []));
  initSymbolSearch('f-symbol', 'symbol-suggestions');
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const data = getTransactionFormData();
    data.account_id = state.currentAccountId;
    await API.post('/api/transactions', data);
    closeModal(); toast('Transaction added', 'success'); loadAccountDetail(state.currentAccountId);
  });
}

async function editTransaction(id) {
  const [tx, tags] = await Promise.all([API.get(`/api/transactions/${id}`), API.get('/api/tags')]);
  openModal('Edit Transaction', transactionFormHtml(tx, tags || []));
  initSymbolSearch('f-symbol', 'symbol-suggestions');
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    await API.put(`/api/transactions/${id}`, getTransactionFormData());
    closeModal(); toast('Transaction updated', 'success'); loadAccountDetail(state.currentAccountId);
  });
}

async function deleteTransaction(id) {
  if (!confirm('Delete this transaction?')) return;
  await API.del(`/api/transactions/${id}`);
  toast('Transaction deleted', 'success'); loadAccountDetail(state.currentAccountId);
}

function showAddTransactionForSymbol() {
  showAddTransactionModal();
  setTimeout(() => {
    const symbolInput = document.getElementById('f-symbol');
    if (symbolInput && state.currentSymbol) symbolInput.value = state.currentSymbol;
  }, 100);
}

// ===== Import / Export =====
async function handleFileSelected() {
  const file = document.getElementById('import-file').files[0];
  const formatEl = document.getElementById('import-format-detected');
  if (!file) { formatEl.classList.add('hidden'); return; }
  const formData = new FormData();
  formData.append('file', file);
  try {
    const res = await fetch('/api/transactions/import/detect', { method: 'POST', body: formData });
    const data = await res.json();
    const formatNames = { 'revolut-stock': 'Revolut Stocks', 'revolut-commodity': 'Revolut Commodities', 'trezor': 'Trezor Wallet', 'generic': 'Generic CSV', 'unknown': 'Unknown Format' };
    const name = formatNames[data.format] || data.format;
    const isKnown = data.format !== 'unknown';
    formatEl.innerHTML = `<span class="format-badge ${isKnown ? 'detected' : 'unknown'}"><i class="fas ${isKnown ? 'fa-check-circle' : 'fa-question-circle'}"></i> Format: ${name}</span>`;
    formatEl.classList.remove('hidden');
  } catch { formatEl.classList.add('hidden'); }
}

async function handleImport(e) {
  e.preventDefault();
  const file = document.getElementById('import-file').files[0];
  if (!file) return;
  const formData = new FormData();
  formData.append('file', file);
  formData.append('account_id', state.currentAccountId);
  try {
    const res = await fetch('/api/transactions/import/csv', { method: 'POST', body: formData });
    const result = await res.json();
    const resultEl = document.getElementById('import-result');
    resultEl.classList.remove('hidden');
    if (result.imported !== undefined) {
      resultEl.innerHTML = `<div class="success-message"><strong>Import complete!</strong><br>Imported: ${result.imported} | Skipped: ${result.skipped} | Total: ${result.total}</div>`;
      loadAccountDetail(state.currentAccountId);
    } else {
      resultEl.innerHTML = `<div class="error-message">${result.error || 'Import failed'}</div>`;
    }
  } catch { toast('Import failed', 'error'); }
}

function exportTransactions() {
  window.location.href = `/api/transactions/export/csv?account_id=${state.currentAccountId}`;
}

// ===== Password =====
async function handlePasswordChange(e) {
  e.preventDefault();
  const msgEl = document.getElementById('password-message');
  const current = document.getElementById('current-password').value;
  const newPw = document.getElementById('new-password').value;
  const confirmPw = document.getElementById('confirm-password').value;
  if (newPw !== confirmPw) { msgEl.className = 'error-message'; msgEl.textContent = 'Passwords do not match'; msgEl.classList.remove('hidden'); return; }
  try {
    const res = await fetch('/api/auth/password', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ currentPassword: current, newPassword: newPw }) });
    const data = await res.json();
    if (res.ok) { msgEl.className = 'success-message'; msgEl.textContent = 'Password updated'; document.getElementById('password-form').reset(); }
    else { msgEl.className = 'error-message'; msgEl.textContent = data.error; }
    msgEl.classList.remove('hidden');
  } catch { msgEl.className = 'error-message'; msgEl.textContent = 'Failed to update'; msgEl.classList.remove('hidden'); }
}

// ===== Purge =====
async function handlePurge() {
  const confirmed = confirm('WARNING: This will permanently delete ALL accounts, transactions, goals, and cached data. This action cannot be undone.\n\nAre you absolutely sure?');
  if (!confirmed) return;
  const doubleConfirm = confirm('Final confirmation: Type OK to proceed.\n\nAll data will be lost permanently.');
  if (!doubleConfirm) return;
  try {
    await API.del('/api/accounts/purge/all');
    state.accounts = [];
    state.allHoldings = [];
    state.dashboardSummary = null;
    toast('All data has been purged', 'success');
    loadSettings();
  } catch { toast('Purge failed', 'error'); }
}

// ===== Sidebar Toggle =====
function toggleSidebar() {
  const sidebar = document.getElementById('sidebar');
  const icon = document.querySelector('#sidebar-toggle i');
  const label = document.querySelector('#sidebar-toggle .sidebar-label');
  sidebar.classList.toggle('expanded');
  const expanded = sidebar.classList.contains('expanded');
  icon.className = expanded ? 'fas fa-angles-left' : 'fas fa-angles-right';
  if (label) label.textContent = expanded ? 'Collapse' : 'Expand';
  localStorage.setItem('wealth-sidebar-expanded', expanded ? '1' : '0');
}

function initSidebar() {
  const expanded = localStorage.getItem('wealth-sidebar-expanded') === '1';
  if (expanded) {
    const sidebar = document.getElementById('sidebar');
    sidebar.classList.add('expanded');
    const icon = document.querySelector('#sidebar-toggle i');
    const label = document.querySelector('#sidebar-toggle .sidebar-label');
    icon.className = 'fas fa-angles-left';
    if (label) label.textContent = 'Collapse';
  }
}

// ===== Drag & Drop CSV Import =====
function initDragAndDrop() {
  // Create drop zone overlay
  let dropOverlay = document.getElementById('csv-drop-overlay');
  if (!dropOverlay) {
    dropOverlay = document.createElement('div');
    dropOverlay.id = 'csv-drop-overlay';
    dropOverlay.className = 'drop-zone-overlay';
    dropOverlay.innerHTML = `
      <div class="drop-zone-content">
        <i class="fas fa-file-csv"></i>
        <h3>Drop CSV to Import</h3>
        <p>Release to import transactions from your CSV file</p>
      </div>`;
    document.body.appendChild(dropOverlay);
  }

  let dragCounter = 0;

  document.addEventListener('dragenter', (e) => {
    e.preventDefault();
    dragCounter++;
    // Check if it's a file drag
    if (e.dataTransfer && e.dataTransfer.types.includes('Files')) {
      dropOverlay.classList.add('active');
    }
  });

  document.addEventListener('dragleave', (e) => {
    e.preventDefault();
    dragCounter--;
    if (dragCounter <= 0) {
      dragCounter = 0;
      dropOverlay.classList.remove('active');
    }
  });

  document.addEventListener('dragover', (e) => {
    e.preventDefault();
  });

  document.addEventListener('drop', async (e) => {
    e.preventDefault();
    dragCounter = 0;
    dropOverlay.classList.remove('active');

    const files = e.dataTransfer?.files;
    if (!files || files.length === 0) return;

    const file = files[0];
    if (!file.name.toLowerCase().endsWith('.csv')) {
      toast('Please drop a .csv file', 'error');
      return;
    }

    // Need an account_id to import into
    // If we're on account detail page, use that account
    // Otherwise, prompt user to select an account
    let accountId = null;

    // Check if we're on the account detail page
    const accountPage = document.getElementById('account-page');
    if (accountPage && accountPage.classList.contains('active') && state.currentAccountId) {
      accountId = state.currentAccountId;
    } else {
      // Show account selection modal
      accountId = await showAccountSelectionForImport();
    }

    if (!accountId) {
      toast('Import cancelled - no account selected', 'info');
      return;
    }

    // Import the file
    toast('Importing CSV...', 'info');
    const formData = new FormData();
    formData.append('file', file);
    formData.append('account_id', accountId);

    try {
      const res = await fetch('/api/transactions/import/csv', { method: 'POST', body: formData });
      const result = await res.json();
      if (result.imported !== undefined) {
        toast(`Import complete! ${result.imported} imported, ${result.skipped} skipped`, 'success');
        // Refresh current view
        if (state.currentAccountId) loadAccountDetail(state.currentAccountId);
        const dashboardPage = document.getElementById('dashboard-page');
        if (dashboardPage && dashboardPage.classList.contains('active')) loadDashboard();
      } else {
        toast(result.error || 'Import failed', 'error');
      }
    } catch {
      toast('Import failed', 'error');
    }
  });
}

async function showAccountSelectionForImport() {
  return new Promise(async (resolve) => {
    const accounts = state.accounts.length ? state.accounts : await API.get('/api/accounts') || [];
    if (!accounts.length) {
      toast('No accounts found. Create an account first.', 'error');
      resolve(null);
      return;
    }

    const html = `
      <div style="margin-bottom:1rem;">
        <p style="font-size:0.875rem;color:var(--text-muted);margin-bottom:0.75rem;">Select an account to import transactions into:</p>
        <div id="import-account-list" style="display:flex;flex-direction:column;gap:0.5rem;">
          ${accounts.map(a => `
            <button class="btn btn-secondary btn-block import-account-btn" data-account-id="${a.id}" style="justify-content:flex-start;gap:0.75rem;padding:0.75rem 1rem;">
              <strong>${esc(a.name)}</strong>
              <span style="color:var(--text-muted);font-size:0.75rem;margin-left:auto;">${a.currency} &middot; ${a.type}</span>
            </button>
          `).join('')}
        </div>
      </div>`;

    openModal('Import CSV - Select Account', html);

    // Bind click handlers
    document.querySelectorAll('.import-account-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const id = parseInt(btn.dataset.accountId);
        closeModal();
        resolve(id);
      });
    });

    // If modal is closed without selecting
    const overlay = document.getElementById('modal-overlay');
    const closeHandler = () => {
      resolve(null);
      overlay.removeEventListener('click', outsideHandler);
    };
    const outsideHandler = (e) => {
      if (e.target === overlay) { closeHandler(); }
    };
    overlay.addEventListener('click', outsideHandler);
  });
}

// ===== Expose functions globally (called from onclick handlers in HTML) =====
window.navigateTo = navigateTo;
window.showAccountPage = showAccountPage;
window.showSymbolPage = showSymbolPage;
window.editTransaction = editTransaction;
window.deleteTransaction = deleteTransaction;
window.showAddTransactionForSymbol = showAddTransactionForSymbol;
window.showEditAccountModal = showEditAccountModal;
window.deleteAccount = deleteAccount;
window.showEditGoalModal = showEditGoalModal;
window.deleteGoal = deleteGoal;
window.showEditRateModal = showEditRateModal;
window.deleteRate = deleteRate;
window.showEditTagModal = showEditTagModal;
window.deleteTag = deleteTag;
