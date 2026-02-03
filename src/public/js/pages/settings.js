/* ===== Settings Page ===== */

import { API } from '../modules/api.js';
import { state, updateSetting, getSetting } from '../modules/state.js';
import { formatMoney, formatDate, esc, toast } from '../modules/utils.js';
import { openModal, closeModal } from '../modules/modal.js';
import { updateThemeButtons } from '../modules/theme.js';
import { accountFormHtml, getAccountFormData, goalFormHtml, getGoalFormData, rateFormHtml, getRateFormData, tagFormHtml, getTagFormData } from '../components/forms.js';

export async function loadSettings() {
  await Promise.all([loadSettingsAccounts(), loadSettingsGoals(), loadSettingsRates(), loadSettingsTags()]);
  updateThemeButtons(document.documentElement.getAttribute('data-theme'));
  initAppearanceToggles();
  initDatabaseSettings();
  loadAboutInfo();
}

async function loadSettingsAccounts() {
  const accounts = await API.get('/api/accounts');
  state.accounts = accounts || [];
  const list = document.getElementById('settings-accounts-list');
  if (!accounts?.length) { list.innerHTML = `<div class="empty-state"><i class="fas fa-wallet"></i><p>No accounts.</p></div>`; return; }
  list.innerHTML = accounts.map(a => {
    const tagBadges = (a.tags || []).map(t => `<span class="tag-badge" style="background:${t.color}20;color:${t.color}">${esc(t.name)}</span>`).join('');
    return `<div class="settings-item"><div class="settings-item-info"><h4>${esc(a.name)}</h4><p>${esc(a.type)} &middot; ${a.currency}${a.description ? ' &middot; ' + esc(a.description) : ''}</p>${tagBadges ? `<div class="settings-item-tags">${tagBadges}</div>` : ''}</div><div class="settings-item-actions"><button class="btn btn-ghost btn-icon btn-sm" onclick="showEditAccountModal(${a.id})" title="Edit"><i class="fas fa-pen"></i></button><button class="btn btn-ghost btn-icon btn-sm text-danger" onclick="deleteAccount(${a.id})" title="Delete"><i class="fas fa-trash"></i></button></div></div>`;
  }).join('');
}

async function loadSettingsGoals() {
  const goals = await API.get('/api/goals');
  const list = document.getElementById('settings-goals-list');
  if (!goals?.length) { list.innerHTML = `<div class="empty-state"><i class="fas fa-bullseye"></i><p>No goals.</p></div>`; return; }
  list.innerHTML = goals.map(g => {
    const progress = g.target_amount > 0 ? Math.min(100, (g.current_amount / g.target_amount) * 100) : 0;
    const statusBadge = g.status && g.status !== 'not_started' ? `<span class="badge badge-status-${g.status}">${g.status.replace(/_/g, ' ')}</span>` : '';
    const tagBadges = (g.tags || []).map(t => `<span class="tag-badge" style="background:${t.color}20;color:${t.color}">${esc(t.name)}</span>`).join('');
    const location = [g.year ? `${g.year}` : '', g.quarter ? `Q${g.quarter}` : '', g.month ? `M${g.month}` : '', g.week ? `W${g.week}` : ''].filter(Boolean).join(' / ');
    return `<div class="settings-item"><div class="settings-item-info"><h4>${g.achieved ? '<i class="fas fa-check text-success"></i> ' : ''}${esc(g.title)} ${statusBadge}</h4><p>${formatMoney(g.current_amount, g.currency)} / ${formatMoney(g.target_amount, g.currency)} (${progress.toFixed(0)}%) &middot; ${formatDate(g.target_date)}${location ? ' &middot; ' + location : ''}</p>${tagBadges ? `<div class="settings-item-tags">${tagBadges}</div>` : ''}</div><div class="settings-item-actions"><button class="btn btn-ghost btn-icon btn-sm" onclick="showEditGoalModal(${g.id})" title="Edit"><i class="fas fa-pen"></i></button><button class="btn btn-ghost btn-icon btn-sm text-danger" onclick="deleteGoal(${g.id})" title="Delete"><i class="fas fa-trash"></i></button></div></div>`;
  }).join('');
}

async function loadSettingsRates() {
  const rates = await API.get('/api/currencies');
  const list = document.getElementById('settings-rates-list');
  if (!rates?.length) { list.innerHTML = `<div class="empty-state"><i class="fas fa-exchange-alt"></i><p>No currency rates.</p></div>`; return; }
  list.innerHTML = rates.map(r => `<div class="settings-item"><div class="settings-item-info"><h4>${esc(r.from_currency)} &rarr; ${esc(r.to_currency)}</h4><p>Rate: ${r.rate}</p></div><div class="settings-item-actions"><button class="btn btn-ghost btn-icon btn-sm" onclick="showEditRateModal(${r.id})" title="Edit"><i class="fas fa-pen"></i></button><button class="btn btn-ghost btn-icon btn-sm text-danger" onclick="deleteRate(${r.id})" title="Delete"><i class="fas fa-trash"></i></button></div></div>`).join('');
}

async function loadSettingsTags() {
  const tags = await API.get('/api/tags');
  const list = document.getElementById('settings-tags-list');
  if (!tags?.length) { list.innerHTML = `<div class="empty-state"><i class="fas fa-tags"></i><p>No tags.</p></div>`; return; }
  list.innerHTML = tags.map(t => `<div class="settings-item"><div class="settings-item-info"><h4><span class="tag-badge" style="background:${t.color}20;color:${t.color}">${esc(t.name)}</span></h4></div><div class="settings-item-actions"><button class="btn btn-ghost btn-icon btn-sm" onclick="showEditTagModal(${t.id})" title="Edit"><i class="fas fa-pen"></i></button><button class="btn btn-ghost btn-icon btn-sm text-danger" onclick="deleteTag(${t.id})" title="Delete"><i class="fas fa-trash"></i></button></div></div>`).join('');
}

async function initAppearanceToggles() {
  // Main currency selector
  const currencySelect = document.getElementById('main-currency-select');
  if (currencySelect) {
    try {
      const session = await API.get('/api/auth/session');
      if (session && session.base_currency) {
        currencySelect.value = session.base_currency;
      }
    } catch (e) {}

    currencySelect.addEventListener('change', async () => {
      try {
        await API.put('/api/auth/currency', { base_currency: currencySelect.value });
        toast('Main currency updated', 'success');
        // Refresh dashboard to show new currency
        window.dispatchEvent(new CustomEvent('currency:changed'));
      } catch (e) {
        toast('Failed to update currency', 'error');
      }
    });
  }

  // Transaction dots toggle
  const toggle = document.getElementById('toggle-tx-dots');
  if (toggle) {
    toggle.classList.toggle('active', getSetting('showTransactionDots'));
    const newToggle = toggle.cloneNode(true);
    toggle.parentNode.replaceChild(newToggle, toggle);
    newToggle.addEventListener('click', () => {
      const isActive = newToggle.classList.toggle('active');
      updateSetting('showTransactionDots', isActive);
    });
  }
}

// ===== Account Modals =====
export async function showAddAccountModal() {
  const tags = await API.get('/api/tags');
  openModal('Add Account', accountFormHtml({}, tags || []));
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    await API.post('/api/accounts', getAccountFormData());
    closeModal(); toast('Account created', 'success');
    // Refresh current view: settings, accounts list, or dashboard
    const settingsPage = document.getElementById('settings-page');
    const accountsListPage = document.getElementById('accounts-list-page');
    if (settingsPage && settingsPage.classList.contains('active')) loadSettings();
    // Dispatch event to refresh accounts list and dashboard
    window.dispatchEvent(new CustomEvent('accounts:changed'));
  });
}

export async function showEditAccountModal(id) {
  const [account, tags] = await Promise.all([API.get(`/api/accounts/${id}`), API.get('/api/tags')]);
  openModal('Edit Account', accountFormHtml(account, tags || []));
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    await API.put(`/api/accounts/${id}`, getAccountFormData());
    closeModal(); toast('Account updated', 'success'); loadSettings();
    window.dispatchEvent(new CustomEvent('accounts:changed'));
  });
}

export async function deleteAccount(id) {
  if (!confirm('Delete this account and all its transactions?')) return;
  await API.del(`/api/accounts/${id}`);
  toast('Account deleted', 'success'); loadSettings();
  window.dispatchEvent(new CustomEvent('accounts:changed'));
}

// ===== Goal Modals =====
export async function showAddGoalModal() {
  const tags = await API.get('/api/tags');
  openModal('Add Goal', goalFormHtml({}, tags || []));
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault(); await API.post('/api/goals', getGoalFormData());
    closeModal(); toast('Goal created', 'success'); loadSettings();
  });
}

export async function showEditGoalModal(id) {
  const [goal, tags] = await Promise.all([API.get(`/api/goals/${id}`), API.get('/api/tags')]);
  openModal('Edit Goal', goalFormHtml(goal, tags || []));
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault(); await API.put(`/api/goals/${id}`, getGoalFormData());
    closeModal(); toast('Goal updated', 'success'); loadSettings();
  });
}

export async function deleteGoal(id) {
  if (!confirm('Delete this goal?')) return;
  await API.del(`/api/goals/${id}`); toast('Goal deleted', 'success'); loadSettings();
}

export async function removeAllGoals() {
  const confirmed = confirm('WARNING: This will permanently delete ALL goals. This action cannot be undone.\n\nAre you sure?');
  if (!confirmed) return;
  const doubleConfirm = confirm('Final confirmation: All goals will be permanently deleted.');
  if (!doubleConfirm) return;
  await API.del('/api/goals');
  toast('All goals removed', 'success'); loadSettings();
}

// ===== Tag Modals =====
export function showAddTagModal() {
  openModal('Add Tag', tagFormHtml());
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    await API.post('/api/tags', getTagFormData());
    closeModal(); toast('Tag created', 'success'); loadSettingsTags();
  });
}

export async function showEditTagModal(id) {
  const tag = await API.get(`/api/tags/${id}`);
  openModal('Edit Tag', tagFormHtml(tag));
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault();
    await API.put(`/api/tags/${id}`, getTagFormData());
    closeModal(); toast('Tag updated', 'success'); loadSettingsTags();
  });
}

export async function deleteTag(id) {
  if (!confirm('Delete this tag?')) return;
  await API.del(`/api/tags/${id}`);
  toast('Tag deleted', 'success'); loadSettingsTags();
}

// ===== Rate Modals =====
export function showAddRateModal() {
  openModal('Add Currency Rate', rateFormHtml());
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault(); await API.post('/api/currencies', getRateFormData());
    closeModal(); toast('Rate added', 'success'); loadSettingsRates();
  });
}

export async function showEditRateModal(id) {
  const rates = await API.get('/api/currencies');
  const rate = rates.find(r => r.id === id);
  openModal('Edit Currency Rate', rateFormHtml(rate));
  document.getElementById('modal-form').addEventListener('submit', async (e) => {
    e.preventDefault(); await API.put(`/api/currencies/${id}`, getRateFormData());
    closeModal(); toast('Rate updated', 'success'); loadSettingsRates();
  });
}

export async function deleteRate(id) {
  if (!confirm('Delete this rate?')) return;
  await API.del(`/api/currencies/${id}`); toast('Rate deleted', 'success'); loadSettingsRates();
}

// ===== Database Settings =====
async function initDatabaseSettings() {
  // Load current database path
  try {
    const dbInfo = await API.get('/api/settings/database');
    const dbPathInput = document.getElementById('db-path-input');
    const dbStatus = document.getElementById('db-status');

    if (dbPathInput && dbInfo) {
      dbPathInput.value = dbInfo.path || '';
      if (dbStatus) {
        if (dbInfo.exists) {
          dbStatus.textContent = 'Database file exists and is accessible.';
          dbStatus.className = 'form-hint success';
        } else {
          dbStatus.textContent = 'Database file will be created at this location.';
          dbStatus.className = 'form-hint';
        }
      }
    }
  } catch (e) {
    console.error('Failed to load database settings:', e);
  }

  // Save database path button
  const saveDbPathBtn = document.getElementById('save-db-path-btn');
  if (saveDbPathBtn) {
    const newBtn = saveDbPathBtn.cloneNode(true);
    saveDbPathBtn.parentNode.replaceChild(newBtn, saveDbPathBtn);
    newBtn.addEventListener('click', async () => {
      const dbPathInput = document.getElementById('db-path-input');
      const dbStatus = document.getElementById('db-status');
      const newPath = dbPathInput?.value?.trim();

      if (!newPath) {
        toast('Please enter a database path', 'error');
        return;
      }

      try {
        const result = await API.put('/api/settings/database', { path: newPath });
        toast('Database path updated', 'success');
        if (dbStatus) {
          dbStatus.textContent = result.message || 'Database path saved successfully.';
          dbStatus.className = 'form-hint success';
        }
      } catch (e) {
        toast('Failed to update database path', 'error');
        if (dbStatus) {
          dbStatus.textContent = e.message || 'Failed to save database path.';
          dbStatus.className = 'form-hint error';
        }
      }
    });
  }

  // Refresh platform button
  const refreshBtn = document.getElementById('refresh-platform-btn');
  if (refreshBtn) {
    const newBtn = refreshBtn.cloneNode(true);
    refreshBtn.parentNode.replaceChild(newBtn, refreshBtn);
    newBtn.addEventListener('click', async () => {
      const confirmed = confirm('This will reload the application with the current database. Continue?');
      if (!confirmed) return;

      try {
        await API.post('/api/settings/refresh');
        toast('Platform refreshed successfully', 'success');
        // Reload the page to reflect changes
        window.location.reload();
      } catch (e) {
        toast('Failed to refresh platform', 'error');
      }
    });
  }
}

// ===== About Section =====
async function loadAboutInfo() {
  try {
    const about = await API.get('/api/settings/about');
    const versionEl = document.getElementById('about-version');
    if (versionEl && about) {
      versionEl.textContent = about.version || '1.0.0';
    }
  } catch (e) {
    console.error('Failed to load about info:', e);
  }
}
