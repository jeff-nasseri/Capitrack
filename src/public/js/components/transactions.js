/* ===== Transaction Components ===== */

import { API } from '../modules/api.js';
import { state } from '../modules/state.js';
import { formatMoney, formatNumber, formatDate, esc, toast, getSymbolIcon, getSymbolIconSmall } from '../modules/utils.js';
import { openModal, closeModal } from '../modules/modal.js';

// Close any open dropdown when clicking elsewhere
document.addEventListener('click', (e) => {
  if (!e.target.closest('.tx-menu-wrapper')) {
    document.querySelectorAll('.tx-dropdown.open').forEach(d => d.classList.remove('open'));
  }
});

/**
 * Render symbol transaction list (Lots tab on symbol detail page)
 */
export function renderSymbolTransactions(transactions) {
  const container = document.getElementById('symbol-transactions');
  if (!transactions.length) {
    container.innerHTML = `<div class="empty-state"><i class="fas fa-receipt"></i><p>No transactions.</p></div>`;
    return;
  }
  container.innerHTML = `<table><thead><tr>
    <th>Date</th><th>Type</th><th>Quantity</th><th>Price</th><th>Fee</th><th>Total</th><th>Notes</th><th></th>
  </tr></thead><tbody>
    ${transactions.map(tx => `<tr>
      <td>${formatDate(tx.date)}</td>
      <td><span class="badge badge-${tx.type}">${tx.type.replace('_', ' ')}</span></td>
      <td>${formatNumber(tx.quantity)}</td>
      <td>${formatMoney(tx.price, tx.currency)}</td>
      <td>${tx.fee > 0 ? formatMoney(tx.fee, tx.currency) : '-'}</td>
      <td>${formatMoney(tx.quantity * tx.price, tx.currency)}</td>
      <td class="text-muted">${esc((tx.notes || '').substring(0, 20))}${(tx.notes || '').length > 20 ? '...' : ''}</td>
      <td>${renderTxMenu(tx)}</td>
    </tr>`).join('')}</tbody></table>`;
  bindTxMenus(container);
}

/**
 * Render activity page (all transactions across accounts)
 */
export function renderActivityTable(transactions, container) {
  if (!transactions?.length) {
    container.innerHTML = `<div class="empty-state"><i class="fas fa-receipt"></i><p>No transactions yet.</p></div>`;
    return;
  }
  container.innerHTML = `<table><thead><tr>
    <th>Date</th><th>Account</th><th>Symbol</th><th>Type</th><th>Quantity</th><th>Price</th><th>Total</th><th></th>
  </tr></thead><tbody>
    ${transactions.map(tx => `<tr>
      <td>${formatDate(tx.date)}</td>
      <td>${esc(tx.account_name)}</td>
      <td><div class="tx-symbol-cell">${getSymbolIconSmall(tx.symbol)}<strong>${esc(tx.symbol)}</strong></div></td>
      <td><span class="badge badge-${tx.type}">${tx.type.replace('_', ' ')}</span></td>
      <td>${formatNumber(tx.quantity)}</td>
      <td>${formatMoney(tx.price, tx.currency)}</td>
      <td>${formatMoney(tx.quantity * tx.price, tx.currency)}</td>
      <td>${renderTxMenu(tx)}</td>
    </tr>`).join('')}</tbody></table>`;
  bindTxMenus(container);
}

/**
 * Render the three-dot menu button for a transaction row
 */
function renderTxMenu(tx) {
  return `<div class="tx-menu-wrapper">
    <button class="tx-menu-trigger" data-tx-id="${tx.id}" onclick="event.stopPropagation()">
      <i class="fas fa-ellipsis-v"></i>
    </button>
    <div class="tx-dropdown" data-tx-id="${tx.id}">
      <button class="tx-dropdown-item" data-action="detail" data-tx='${esc(JSON.stringify(tx))}'>
        <i class="fas fa-info-circle"></i> View Details
      </button>
      <button class="tx-dropdown-item" data-action="edit" data-tx-id="${tx.id}">
        <i class="fas fa-pen"></i> Edit
      </button>
      <button class="tx-dropdown-item danger" data-action="delete" data-tx-id="${tx.id}">
        <i class="fas fa-trash"></i> Delete
      </button>
    </div>
  </div>`;
}

/**
 * Bind click handlers to all tx menus in a container
 */
function bindTxMenus(container) {
  // Toggle dropdown
  container.querySelectorAll('.tx-menu-trigger').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      const id = btn.dataset.txId;
      // Close all other dropdowns
      document.querySelectorAll('.tx-dropdown.open').forEach(d => {
        d.classList.remove('open');
        d.classList.remove('dropup');
      });
      const dropdown = container.querySelector(`.tx-dropdown[data-tx-id="${id}"]`);
      if (dropdown) {
        // Check if dropdown would go off-screen at the bottom
        const btnRect = btn.getBoundingClientRect();
        const dropdownHeight = 120; // Approximate height of dropdown
        const viewportHeight = window.innerHeight;

        // If there's not enough space below, show dropdown above
        if (btnRect.bottom + dropdownHeight > viewportHeight) {
          dropdown.classList.add('dropup');
        } else {
          dropdown.classList.remove('dropup');
        }
        dropdown.classList.toggle('open');
      }
    });
  });

  // Dropdown actions
  container.querySelectorAll('.tx-dropdown-item').forEach(item => {
    item.addEventListener('click', (e) => {
      e.stopPropagation();
      const action = item.dataset.action;
      const txId = item.dataset.txId;
      // Close dropdown
      item.closest('.tx-dropdown').classList.remove('open');

      if (action === 'detail') {
        try {
          const tx = JSON.parse(item.dataset.tx);
          showTransactionDetail(tx);
        } catch { /* ignore parse errors */ }
      } else if (action === 'edit') {
        window.editTransaction(parseInt(txId));
      } else if (action === 'delete') {
        window.deleteTransaction(parseInt(txId));
      }
    });
  });
}

/**
 * Show transaction detail in a modal
 */
function showTransactionDetail(tx) {
  const total = tx.quantity * tx.price;
  const feeStr = tx.fee > 0 ? formatMoney(tx.fee, tx.currency) : 'None';
  const html = `
    <div style="display:flex;align-items:center;gap:0.75rem;margin-bottom:1.25rem;">
      ${getSymbolIcon(tx.symbol)}
      <div>
        <h3 style="font-weight:700;font-size:1.125rem;">${esc(tx.symbol)}</h3>
        <span class="badge badge-${tx.type}" style="font-size:0.75rem;">${tx.type.replace('_', ' ')}</span>
      </div>
    </div>
    <div class="tx-detail-grid">
      <div class="tx-detail-item">
        <div class="label">Date</div>
        <div class="value">${formatDate(tx.date)}</div>
      </div>
      <div class="tx-detail-item">
        <div class="label">Type</div>
        <div class="value" style="text-transform:capitalize;">${tx.type.replace('_', ' ')}</div>
      </div>
      <div class="tx-detail-item">
        <div class="label">Quantity</div>
        <div class="value">${formatNumber(tx.quantity)}</div>
      </div>
      <div class="tx-detail-item">
        <div class="label">Price</div>
        <div class="value">${formatMoney(tx.price, tx.currency)}</div>
      </div>
      <div class="tx-detail-item">
        <div class="label">Total Value</div>
        <div class="value">${formatMoney(total, tx.currency)}</div>
      </div>
      <div class="tx-detail-item">
        <div class="label">Fee</div>
        <div class="value">${feeStr}</div>
      </div>
      <div class="tx-detail-item">
        <div class="label">Currency</div>
        <div class="value">${esc(tx.currency || 'USD')}</div>
      </div>
      <div class="tx-detail-item">
        <div class="label">Account</div>
        <div class="value">${esc(tx.account_name || 'N/A')}</div>
      </div>
      ${tx.notes ? `<div class="tx-detail-item tx-detail-full">
        <div class="label">Notes</div>
        <div class="value">${esc(tx.notes)}</div>
      </div>` : ''}
    </div>`;
  openModal('Transaction Details', html);
}

/**
 * Transaction form HTML generator
 */
export function transactionFormHtml(tx = {}, tags = []) {
  const tagCheckboxes = tags.map(t =>
    `<label class="tag-checkbox" style="--tag-color:${t.color}"><input type="checkbox" value="${t.id}" ${(tx.tags || []).some(tt => tt.id === t.id) ? 'checked' : ''}><span class="tag-chip">${esc(t.name)}</span></label>`
  ).join('');

  return `<form id="modal-form">
    <div class="form-group symbol-search-wrapper">
      <label>Symbol</label>
      <input type="text" id="f-symbol" value="${esc(tx.symbol || '')}" required placeholder="Search: BTC-USD, AAPL, GC=F..." autocomplete="off">
      <div id="symbol-suggestions" class="symbol-suggestions hidden"></div>
    </div>
    <div class="form-row">
      <div class="form-group"><label>Type</label><select id="f-tx-type">${['buy','sell','transfer_in','transfer_out','dividend','interest','fee'].map(t => `<option value="${t}" ${tx.type === t ? 'selected' : ''}>${t.replace('_', ' ')}</option>`).join('')}</select></div>
      <div class="form-group"><label>Date</label><input type="date" id="f-date" value="${tx.date ? tx.date.split('T')[0] : new Date().toISOString().split('T')[0]}" required></div>
    </div>
    <div class="form-row">
      <div class="form-group"><label>Quantity</label><input type="number" id="f-quantity" value="${tx.quantity || ''}" step="any" min="0" required></div>
      <div class="form-group"><label>Price per unit</label><input type="number" id="f-price" value="${tx.price || ''}" step="any" min="0" required></div>
    </div>
    <div class="form-row">
      <div class="form-group"><label>Fee</label><input type="number" id="f-fee" value="${tx.fee || 0}" step="any" min="0"></div>
      <div class="form-group"><label>Currency</label><select id="f-tx-currency">
        ${['EUR', 'USD', 'GBP', 'CHF', 'JPY', 'CAD', 'AUD', 'CNY'].map(c => `<option value="${c}" ${(tx.currency || 'EUR') === c ? 'selected' : ''}>${c}</option>`).join('')}
      </select></div>
    </div>
    <div class="form-group"><label>Notes</label><input type="text" id="f-notes" value="${esc(tx.notes || '')}"></div>
    ${tags.length ? `<div class="form-group"><label>Tags</label><div class="tag-checkbox-group" id="f-tags">${tagCheckboxes}</div></div>` : ''}
    <button type="submit" class="btn btn-primary btn-block"><i class="fas fa-save"></i> Save</button></form>`;
}

export function getTransactionFormData() {
  const tagIds = [];
  const tagsContainer = document.getElementById('f-tags');
  if (tagsContainer) {
    tagsContainer.querySelectorAll('input[type="checkbox"]:checked').forEach(cb => {
      tagIds.push(parseInt(cb.value));
    });
  }
  return {
    symbol: document.getElementById('f-symbol').value.toUpperCase(),
    type: document.getElementById('f-tx-type').value,
    date: document.getElementById('f-date').value,
    quantity: parseFloat(document.getElementById('f-quantity').value),
    price: parseFloat(document.getElementById('f-price').value),
    fee: parseFloat(document.getElementById('f-fee').value) || 0,
    currency: document.getElementById('f-tx-currency').value,
    notes: document.getElementById('f-notes').value,
    tag_ids: tagIds
  };
}
