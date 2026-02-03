/* ===== Holdings Component ===== */

import { formatMoney, formatNumber, esc, getSymbolIcon } from '../modules/utils.js';

/**
 * Render Wealthfolio-style holdings table
 */
export function renderHoldingsTable(holdings, container, accountId) {
  if (!holdings.length) {
    container.innerHTML = `<div class="empty-state"><i class="fas fa-coins"></i><p>No holdings. Add transactions to see your portfolio.</p></div>`;
    return;
  }

  container.innerHTML = `
    <table class="holdings-table">
      <colgroup>
        <col class="col-position">
        <col class="col-shares">
        <col class="col-price">
        <col class="col-value">
        <col class="col-gain">
        <col class="col-arrow">
      </colgroup>
      <thead>
        <tr>
          <th>Position</th>
          <th class="text-right">Shares</th>
          <th class="text-right">Today's Price</th>
          <th class="text-right">Total Value</th>
          <th class="text-right">Total Gain/Loss</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        ${holdings.map(h => {
          const acctId = accountId || h.account_id;
          return `<tr onclick="showSymbolPage(${acctId}, '${esc(h.symbol)}')">
            <td>
              <div class="holding-name-cell">
                <div class="h-icon">${getSymbolIcon(h.symbol)}</div>
                <div class="h-name">
                  <h4>${esc(h.symbol)}</h4>
                  <span>${esc(h.name)}</span>
                </div>
              </div>
            </td>
            <td class="text-right">${formatNumber(h.quantity)}</td>
            <td class="text-right">
              ${formatMoney(h.price, h.currency)}<br>
              <small class="${h.change_percent >= 0 ? 'text-success' : 'text-danger'}">${h.change_percent >= 0 ? '+' : ''}${h.change_percent.toFixed(2)}%</small>
            </td>
            <td class="text-right">
              ${formatMoney(h.market_value, h.currency)}<br>
              <small class="text-muted">${h.currency}</small>
            </td>
            <td class="text-right ${h.gain >= 0 ? 'positive' : 'negative'}">
              ${h.gain >= 0 ? '+' : ''}${formatMoney(h.gain, h.currency)}<br>
              <small>${h.gain_pct >= 0 ? '+' : ''}${h.gain_pct.toFixed(2)}%</small>
            </td>
            <td class="holding-arrow"><i class="fas fa-chevron-right"></i></td>
          </tr>`;
        }).join('')}
      </tbody>
    </table>`;
}
