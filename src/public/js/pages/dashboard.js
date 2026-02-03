/* ===== Dashboard Page ===== */

import { API } from '../modules/api.js';
import { state } from '../modules/state.js';
import { formatMoney, formatNumber, esc, getSymbolIcon, getSymbolColor, getSymbolInitials } from '../modules/utils.js';
import { renderLineChart } from '../components/chart.js';

function updateCETClock() {
  const el = document.getElementById('dashboard-cet-clock');
  if (!el) return;
  const now = new Date();
  const cetStr = now.toLocaleString('en-GB', { timeZone: 'Europe/Berlin', weekday: 'short', year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
  el.textContent = cetStr + ' CET';
}

let cetInterval = null;

export async function loadDashboard() {
  // Show loading states
  document.getElementById('hero-total-wealth').innerHTML = '<div class="skeleton skeleton-line h-lg w-60" style="display:inline-block;min-width:120px;"></div>';
  document.getElementById('accounts-list-dashboard').innerHTML = '<div class="loading-spinner sm">Loading accounts...</div>';
  document.getElementById('holdings-sidebar').innerHTML = '<div class="loading-spinner sm">Loading holdings...</div>';
  document.getElementById('goals-sidebar').innerHTML = '<div class="loading-spinner sm">Loading goals...</div>';

  // CET clock
  let clockEl = document.getElementById('dashboard-cet-clock');
  if (!clockEl) {
    clockEl = document.createElement('div');
    clockEl.id = 'dashboard-cet-clock';
    clockEl.className = 'dashboard-cet-clock';
    const heroEl = document.querySelector('.dashboard-hero');
    if (heroEl) heroEl.appendChild(clockEl);
  }
  updateCETClock();
  if (cetInterval) clearInterval(cetInterval);
  cetInterval = setInterval(updateCETClock, 1000);

  try {
    const [summary, accounts, goals] = await Promise.all([
      API.get('/api/prices/dashboard/summary'),
      API.get('/api/accounts'),
      API.get('/api/goals')
    ]);
    state.accounts = accounts || [];
    state.dashboardSummary = summary;

    if (summary) {
      const cur = summary.base_currency || 'USD';
      document.getElementById('hero-total-wealth').textContent = formatMoney(summary.total_wealth, cur);
      const gain = summary.total_gain || 0;
      const pct = summary.total_gain_percent || 0;
      const heroChange = document.getElementById('hero-change');
      const heroPct = document.getElementById('hero-change-pct');
      heroChange.textContent = `${gain >= 0 ? '+' : ''}${formatMoney(gain, cur)}`;
      heroChange.className = `hero-change ${gain >= 0 ? '' : 'negative'}`;
      heroPct.textContent = `${pct >= 0 ? '+' : ''}${pct.toFixed(2)}%`;
      heroPct.className = `hero-change-pct ${pct >= 0 ? '' : 'negative'}`;
    }

    renderDashboardAccounts(accounts || [], summary);
    renderDashboardHoldings(summary);
    renderDashboardGoals(goals || []);
    loadDashboardChart();

    // Also snapshot today's wealth for calendar
    API.post('/api/prices/daily-wealth', {}).catch(() => {});
  } catch (e) { console.error('Dashboard error:', e); }

  // Hide loading overlay
  const loadingOverlay = document.getElementById('dashboard-loading');
  if (loadingOverlay) loadingOverlay.style.display = 'none';
}

export async function loadDashboardChart(period) {
  if (!period) period = document.querySelector('#dashboard-periods .period-btn.active')?.dataset.period || '3m';
  // Show chart loading overlay
  const chartWrapper = document.querySelector('.chart-wrapper-dashboard');
  let overlay = chartWrapper.querySelector('.chart-loading-overlay');
  if (!overlay) {
    overlay = document.createElement('div');
    overlay.className = 'chart-loading-overlay';
    overlay.innerHTML = '<div class="loading-spinner sm">Loading chart...</div>';
    chartWrapper.appendChild(overlay);
  }
  overlay.classList.add('active');
  try {
    const [data, allTx] = await Promise.all([
      API.get(`/api/prices/portfolio/history?period=${period}`),
      API.get('/api/transactions?limit=500')
    ]);
    renderLineChart('dashboard-chart', data || [], 'dashboardChart', 220, allTx || []);
    const labels = { '1w': 'Last week', '1m': 'Last month', '3m': 'Last 3 months', '6m': 'Last 6 months', 'ytd': 'Year to date', '1y': 'Last year', '5y': 'Last 5 years', 'all': 'All time' };
    document.getElementById('hero-period').textContent = labels[period] || '';
  } catch (e) { console.error('Dashboard chart error:', e); }
  overlay.classList.remove('active');
}

function renderDashboardAccounts(accounts, summary) {
  const container = document.getElementById('accounts-list-dashboard');
  if (!accounts.length) { container.innerHTML = `<div class="empty-state"><i class="fas fa-wallet"></i><p>No accounts yet.</p></div>`; return; }
  const accountValueMap = {};
  for (const sa of (summary?.accounts || [])) accountValueMap[sa.account_id] = sa;

  container.innerHTML = accounts.map(a => {
    const sa = accountValueMap[a.id] || {};
    const value = sa.market_value || 0;
    const cost = sa.cost_basis || 0;
    const gain = value - cost;
    const gainPct = cost > 0 ? ((gain / cost) * 100) : 0;
    return `
      <div class="account-row" onclick="showAccountPage(${a.id})">
        <div class="account-row-name">
          <h4>${esc(a.name)}</h4>
          <span>${a.currency}</span>
        </div>
        <div class="account-row-value">
          <div class="value">${formatMoney(value, a.currency)}</div>
          <div class="sub">
            <span class="cost">${formatMoney(cost, a.currency)}</span>
            <span class="pct ${gainPct >= 0 ? 'positive' : 'negative'}">${gainPct >= 0 ? '+' : ''}${gainPct.toFixed(1)}%</span>
          </div>
        </div>
        <div class="account-row-arrow"><i class="fas fa-chevron-right"></i></div>
      </div>`;
  }).join('');
}

function renderDashboardHoldings(summary) {
  const container = document.getElementById('holdings-sidebar');
  if (!summary?.accounts?.length) { container.innerHTML = `<div class="empty-state" style="padding:1rem;"><p>No holdings yet.</p></div>`; return; }
  container.innerHTML = '<div style="color:var(--text-dim);font-size:0.8125rem;padding:0.5rem 0;">Loading holdings...</div>';
  loadTopHoldings(container);
}

async function loadTopHoldings(container) {
  try {
    const accounts = state.accounts;
    let allHoldings = [];
    for (const account of accounts) {
      const holdings = await API.get(`/api/accounts/${account.id}/holdings`);
      if (holdings) for (const h of holdings) allHoldings.push({ ...h, account_id: account.id, account_currency: account.currency });
    }
    const symbols = [...new Set(allHoldings.map(h => h.symbol))];
    let prices = {};
    if (symbols.length) prices = await API.post('/api/prices/quotes', { symbols }) || {};

    const enriched = allHoldings.map(h => {
      const p = prices[h.symbol] || {};
      const marketValue = h.quantity * (p.price || 0);
      const costBasis = h.quantity * (h.avg_cost || 0);
      const gain = marketValue - costBasis;
      const gainPct = costBasis > 0 ? (gain / costBasis) * 100 : 0;
      return { ...h, price: p.price || 0, name: p.name || h.symbol, change_percent: p.change_percent || 0, market_value: marketValue, cost_basis: costBasis, gain, gain_pct: gainPct, currency: p.currency || 'USD' };
    }).sort((a, b) => b.market_value - a.market_value);

    state.allHoldings = enriched;
    const top5 = enriched.slice(0, 5);
    const remaining = enriched.slice(5);

    let html = top5.map(h => `
      <div class="holding-row" onclick="showSymbolPage(${h.account_id}, '${esc(h.symbol)}')">
        <div class="holding-row-icon">${getSymbolIcon(h.symbol)}</div>
        <div class="holding-row-info">
          <h4>${esc(h.symbol)}</h4>
          <span>${formatNumber(h.quantity)} shares</span>
        </div>
        <div class="holding-row-values">
          <div class="val">${formatMoney(h.market_value, h.currency)}</div>
          <div class="change ${h.gain >= 0 ? 'positive' : 'negative'}">${h.gain >= 0 ? '+' : ''}${formatMoney(h.gain, h.currency)} ${h.gain_pct >= 0 ? '+' : ''}${h.gain_pct.toFixed(1)}%</div>
        </div>
      </div>
    `).join('');

    if (remaining.length > 0) {
      const miniIcons = remaining.slice(0, 4).map(h => {
        const c = getSymbolColor(h.symbol);
        const init = getSymbolInitials(h.symbol);
        return `<div class="mini-icon" style="background:${c}">${init}</div>`;
      }).join('');
      html += `<div class="holdings-more" onclick="navigateTo('holdings')">
        <div class="holdings-more-icons">${miniIcons}</div>
        <span>+${remaining.length} more holdings</span>
      </div>`;
    }

    container.innerHTML = html || `<div class="empty-state" style="padding:1rem;"><p>No holdings yet.</p></div>`;
  } catch (e) {
    console.error('Holdings sidebar error:', e);
    container.innerHTML = `<div class="empty-state" style="padding:1rem;"><p>Could not load holdings.</p></div>`;
  }
}

function renderDashboardGoals(goals) {
  const container = document.getElementById('goals-sidebar');
  if (!goals.length) { container.innerHTML = `<div class="empty-state" style="padding:0.5rem;font-size:0.8125rem;"><p>No goals set.</p></div>`; return; }
  container.innerHTML = goals.slice(0, 3).map(g => {
    const progress = g.target_amount > 0 ? Math.min(100, (g.current_amount / g.target_amount) * 100) : 0;
    return `<div class="goal-mini"><h4>${esc(g.title)}</h4><div class="goal-mini-bar"><div class="goal-mini-fill" style="width:${progress}%"></div></div></div>`;
  }).join('');
}
