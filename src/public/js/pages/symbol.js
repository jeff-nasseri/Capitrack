/* ===== Symbol Detail Page ===== */

import { API } from '../modules/api.js';
import { state } from '../modules/state.js';
import { formatMoney, formatNumber, formatCompact, formatDate, esc } from '../modules/utils.js';
import { renderLineChart } from '../components/chart.js';
import { renderSymbolTransactions } from '../components/transactions.js';

export async function loadSymbolDetail(accountId, symbol) {
  try {
    const [quote, transactions] = await Promise.all([
      API.get(`/api/prices/quote/${symbol}`),
      API.get(`/api/transactions?account_id=${accountId}&symbol=${symbol}`)
    ]);

    const name = quote?.name || symbol;
    document.getElementById('symbol-title').textContent = name;
    document.getElementById('symbol-ticker').textContent = symbol;

    // Hide image logo (using initials-based icons instead)
    const logoEl = document.getElementById('symbol-logo');
    logoEl.style.display = 'none';

    // Calculate holdings
    let totalQty = 0, totalCost = 0, totalBuyQty = 0;
    for (const tx of (transactions || [])) {
      if (['buy', 'transfer_in'].includes(tx.type)) { totalQty += tx.quantity; totalCost += tx.quantity * tx.price; totalBuyQty += tx.quantity; }
      else if (['sell', 'transfer_out'].includes(tx.type)) { totalQty -= tx.quantity; }
    }

    const price = quote?.price || 0;
    const currency = quote?.currency || 'USD';
    const marketValue = totalQty * price;
    const avgCost = totalBuyQty > 0 ? totalCost / totalBuyQty : 0;
    const bookValue = totalQty * avgCost;
    const totalReturn = marketValue - bookValue;
    const totalReturnPct = bookValue > 0 ? (totalReturn / bookValue) * 100 : 0;
    const todayReturn = totalQty * (price * (quote?.change_percent || 0) / 100);
    const todayReturnPct = quote?.change_percent || 0;

    const totalWealth = state.dashboardSummary?.total_wealth || marketValue;
    const portfolioPct = totalWealth > 0 ? (marketValue / totalWealth) * 100 : 0;

    // Hero price
    document.getElementById('symbol-hero-price').textContent = formatMoney(price, currency);
    const changePct = quote?.change_percent || 0;
    const changeEl = document.getElementById('symbol-hero-change');
    const priceChange = price * changePct / 100;
    changeEl.innerHTML = `<span>${priceChange >= 0 ? '+' : ''}${formatMoney(Math.abs(priceChange), currency)} (${changePct >= 0 ? '+' : ''}${changePct.toFixed(2)}%) past 3 months</span>`;
    changeEl.className = `symbol-hero-change ${changePct >= 0 ? 'positive' : ''}`;

    // Holding card
    document.getElementById('shc-qty').textContent = formatNumber(totalQty);
    document.getElementById('shc-value').textContent = formatMoney(marketValue, currency);
    document.getElementById('shc-currency').textContent = currency;
    document.getElementById('shc-book').textContent = formatMoney(bookValue, currency);
    document.getElementById('shc-avg-cost').textContent = formatMoney(avgCost, currency);
    document.getElementById('shc-portfolio-pct').textContent = portfolioPct.toFixed(2) + '%';

    const todayEl = document.getElementById('shc-today-return');
    todayEl.textContent = `${todayReturn >= 0 ? '+' : ''}${formatMoney(todayReturn, currency)} (${todayReturnPct >= 0 ? '+' : ''}${todayReturnPct.toFixed(2)}%)`;
    todayEl.className = todayReturn >= 0 ? 'text-success' : 'text-danger';

    const totalEl = document.getElementById('shc-total-return');
    totalEl.textContent = `${totalReturn >= 0 ? '+' : ''}${formatMoney(totalReturn, currency)} (${totalReturnPct >= 0 ? '+' : ''}${totalReturnPct.toFixed(2)}%)`;
    totalEl.className = totalReturn >= 0 ? 'text-success' : 'text-danger';

    await loadSymbolChart(symbol, undefined, transactions);
    renderSymbolTransactions(transactions || []);
    renderSymbolAbout(quote, price, currency);
    renderQuoteDetails(quote);
  } catch (e) { console.error('Symbol detail error:', e); }
}

export async function loadSymbolChart(symbol, period, transactions) {
  if (!period) period = document.querySelector('#symbol-periods .period-btn.active')?.dataset.period || '3m';
  try {
    const data = await API.get(`/api/prices/history/${symbol}?period=${period}`);
    if (!data?.length) { if (state.symbolChart) { state.symbolChart.destroy(); state.symbolChart = null; } return; }
    const chartData = data.map(d => ({ date: d.date, value: d.close }));
    // Fetch transactions if not provided
    if (!transactions) {
      transactions = await API.get(`/api/transactions?account_id=${state.currentAccountId}&symbol=${symbol}`) || [];
    }
    renderLineChart('symbol-chart', chartData, 'symbolChart', 250, transactions);
  } catch (e) { console.error('Symbol chart error:', e); }
}

function renderSymbolAbout(quote, price, currency) {
  const tagsEl = document.getElementById('symbol-tags');
  const descEl = document.getElementById('symbol-description');
  const tags = [];
  if (quote?.quoteType) tags.push(quote.quoteType);
  else tags.push('STOCK');
  if (quote?.exchange) tags.push(quote.exchange);

  tagsEl.innerHTML = tags.map(t => `<span class="symbol-tag">${esc(t)}</span>`).join('');
  descEl.textContent = quote?.longBusinessSummary || quote?.name || '';

  const statsEl = document.getElementById('symbol-price-stats');
  statsEl.innerHTML = `
    <div class="price-stat"><span class="label">Open</span><span class="value">${formatMoney(quote?.open || price, currency)}</span></div>
    <div class="price-stat"><span class="label">Close</span><span class="value">${formatMoney(price, currency)}</span></div>
    <div class="price-stat"><span class="label">High</span><span class="value">${formatMoney(quote?.dayHigh || price, currency)}</span></div>
    <div class="price-stat"><span class="label">Low</span><span class="value ${(quote?.dayLow || price) < price ? 'danger' : ''}">${formatMoney(quote?.dayLow || price, currency)}</span></div>
    <div class="price-stat"><span class="label">Prev. Close</span><span class="value">${formatMoney(quote?.previousClose || price, currency)}</span></div>
    <div class="price-stat"><span class="label">Volume</span><span class="value">${formatCompact(quote?.volume || 0)}</span></div>
  `;
}

function renderQuoteDetails(quote) {
  const container = document.getElementById('symbol-quote-details');
  if (!quote) { container.innerHTML = '<p class="text-muted">No quote data available.</p>'; return; }
  const fields = [
    ['Market Cap', formatCompact(quote.marketCap || 0)],
    ['P/E Ratio', quote.trailingPE ? quote.trailingPE.toFixed(2) : 'N/A'],
    ['52W High', formatMoney(quote.fiftyTwoWeekHigh || 0, quote.currency || 'USD')],
    ['52W Low', formatMoney(quote.fiftyTwoWeekLow || 0, quote.currency || 'USD')],
    ['Avg Volume', formatCompact(quote.averageDailyVolume3Month || 0)],
    ['Dividend Yield', quote.dividendYield ? (quote.dividendYield * 100).toFixed(2) + '%' : 'N/A'],
  ];
  container.innerHTML = fields.map(([label, value]) => `<div class="price-stat"><span class="label">${label}</span><span class="value">${value}</span></div>`).join('');
}
