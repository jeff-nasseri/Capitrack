/* ===== Account Detail Page ===== */

import { API } from '../modules/api.js';
import { state } from '../modules/state.js';
import { formatMoney, esc } from '../modules/utils.js';
import { renderLineChart } from '../components/chart.js';
import { renderHoldingsTable } from '../components/holdings.js';

export async function loadAccountDetail(accountId) {
  try {
    const [account, holdings, transactions, currencyRates, session] = await Promise.all([
      API.get(`/api/accounts/${accountId}`),
      API.get(`/api/accounts/${accountId}/holdings`),
      API.get(`/api/transactions?account_id=${accountId}`),
      API.get('/api/currencies'),
      API.get('/api/auth/session')
    ]);
    if (!account) return;

    // Build currency rate lookup
    const rates = {};
    for (const r of (currencyRates || [])) {
      rates[`${r.from_currency}_${r.to_currency}`] = r.rate;
    }

    // Use user's base currency for display, fallback to account currency
    const baseCurrency = session?.base_currency || account.currency;

    document.getElementById('account-title').textContent = account.name;
    document.getElementById('account-currency-badge').textContent = baseCurrency;

    const symbols = holdings.map(h => h.symbol);
    let prices = {};
    if (symbols.length) prices = await API.post('/api/prices/quotes', { symbols }) || {};

    let totalValue = 0, totalCost = 0, totalInvested = 0, totalContribution = 0;
    const enrichedHoldings = holdings.map(h => {
      const p = prices[h.symbol] || {};
      let marketValue = h.quantity * (p.price || 0);
      let costBasis = h.quantity * (h.avg_cost || 0);

      // Convert market value from price currency to base currency
      const priceCurrency = p.currency || 'USD';
      if (priceCurrency !== baseCurrency) {
        const rateKey = `${priceCurrency}_${baseCurrency}`;
        const rate = rates[rateKey] || 1;
        marketValue *= rate;
      }

      // Convert cost basis from account currency to base currency
      if (account.currency !== baseCurrency) {
        const rateKey = `${account.currency}_${baseCurrency}`;
        const rate = rates[rateKey] || 1;
        costBasis *= rate;
      }

      const gain = marketValue - costBasis;
      const gainPct = costBasis > 0 ? (gain / costBasis) * 100 : 0;
      totalValue += marketValue;
      totalCost += costBasis;
      return { ...h, price: p.price || 0, name: p.name || h.symbol, change_percent: p.change_percent || 0, market_value: marketValue, cost_basis: costBasis, gain, gain_pct: gainPct, currency: baseCurrency };
    }).sort((a, b) => b.market_value - a.market_value);

    for (const tx of (transactions || [])) {
      let txValue = tx.quantity * tx.price;
      // Convert transaction values from account currency to base currency
      if (account.currency !== baseCurrency) {
        const rateKey = `${account.currency}_${baseCurrency}`;
        const rate = rates[rateKey] || 1;
        txValue *= rate;
      }
      if (['buy'].includes(tx.type)) { totalInvested += txValue; totalContribution += txValue; }
      if (['sell'].includes(tx.type)) { totalContribution -= txValue; }
    }

    const gain = totalValue - totalCost;
    const gainPct = totalCost > 0 ? (gain / totalCost) * 100 : 0;

    document.getElementById('account-hero-value').textContent = formatMoney(totalValue, baseCurrency);
    const changeEl = document.getElementById('account-hero-change');
    changeEl.innerHTML = `
      <span class="hero-change ${gain >= 0 ? '' : 'negative'}">${gain >= 0 ? '+' : ''}${formatMoney(gain, baseCurrency)}</span>
      <span class="hero-change-pct ${gainPct >= 0 ? '' : 'negative'}">${gainPct >= 0 ? '+' : ''}${gainPct.toFixed(2)}%</span>
    `;

    document.getElementById('metric-investments').textContent = formatMoney(totalInvested, baseCurrency);
    document.getElementById('metric-contribution').textContent = formatMoney(totalContribution, baseCurrency);
    document.getElementById('metric-cost-basis').textContent = formatMoney(totalCost, baseCurrency);

    renderHoldingsTable(enrichedHoldings, document.getElementById('account-holdings-table'), accountId);
    loadAccountChart(accountId, undefined, transactions);
  } catch (e) { console.error('Account detail error:', e); }
}

export async function loadAccountChart(accountId, period, transactions) {
  if (!period) period = document.querySelector('#account-periods .period-btn.active')?.dataset.period || '3m';
  try {
    const data = await API.get(`/api/prices/portfolio/history?account_id=${accountId}&period=${period}`);
    // Fetch transactions if not provided
    if (!transactions) transactions = await API.get(`/api/transactions?account_id=${accountId}`) || [];
    renderLineChart('account-chart', data || [], 'accountChart', 200, transactions);
  } catch (e) { console.error('Account chart error:', e); }
}
