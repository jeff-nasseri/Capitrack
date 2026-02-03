/* ===== Form Components (Accounts, Goals, Rates, Categories, Tags, Symbol Search) ===== */

import { API } from '../modules/api.js';
import { esc } from '../modules/utils.js';

// ===== Account Form =====
export function accountFormHtml(a = {}, tags = []) {
  const tagCheckboxes = tags.map(t =>
    `<label class="tag-checkbox" style="--tag-color:${t.color}"><input type="checkbox" value="${t.id}" ${(a.tags || []).some(at => at.id === t.id) ? 'checked' : ''}><span class="tag-chip">${esc(t.name)}</span></label>`
  ).join('');

  return `<form id="modal-form">
    <div class="form-group"><label>Name</label><input type="text" id="f-name" value="${esc(a.name || '')}" required></div>
    <div class="form-row"><div class="form-group"><label>Type</label><select id="f-type">${['general','crypto','stock','commodity','savings','retirement','real_estate'].map(t => `<option value="${t}" ${a.type === t ? 'selected' : ''}>${t}</option>`).join('')}</select></div><div class="form-group"><label>Currency</label><input type="text" id="f-currency" value="${esc(a.currency || 'EUR')}" maxlength="5"></div></div>
    <div class="form-row"><div class="form-group"><label>Icon</label><select id="f-icon">${['wallet','bitcoin','chart-line','gem','bank','piggy'].map(i => `<option value="${i}" ${a.icon === i ? 'selected' : ''}>${i}</option>`).join('')}</select></div><div class="form-group"><label>Color</label><input type="color" id="f-color" value="${a.color || '#6366f1'}"></div></div>
    <div class="form-group"><label>Description</label><input type="text" id="f-description" value="${esc(a.description || '')}"></div>
    ${tags.length ? `<div class="form-group"><label>Tags (optional)</label><div class="tag-checkboxes" id="f-account-tags">${tagCheckboxes}</div></div>` : ''}
    <button type="submit" class="btn btn-primary btn-block"><i class="fas fa-save"></i> Save</button></form>`;
}

export function getAccountFormData() {
  const data = {
    name: document.getElementById('f-name').value,
    type: document.getElementById('f-type').value,
    currency: document.getElementById('f-currency').value.toUpperCase(),
    icon: document.getElementById('f-icon').value,
    color: document.getElementById('f-color').value,
    description: document.getElementById('f-description').value
  };

  const tagCheckboxes = document.querySelectorAll('#f-account-tags input[type="checkbox"]:checked');
  if (tagCheckboxes.length || document.getElementById('f-account-tags')) {
    data.tag_ids = Array.from(tagCheckboxes).map(cb => parseInt(cb.value));
  }

  return data;
}

// ===== Goal Form =====
export function goalFormHtml(g = {}, tags = []) {
  const monthNames = ['', 'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  const currentYear = new Date().getFullYear();

  const tagCheckboxes = tags.map(t =>
    `<label class="tag-checkbox" style="--tag-color:${t.color}"><input type="checkbox" value="${t.id}" ${(g.tags || []).some(gt => gt.id === t.id) ? 'checked' : ''}><span class="tag-chip">${esc(t.name)}</span></label>`
  ).join('');

  return `<form id="modal-form">
    <div class="form-group"><label>Title</label><input type="text" id="f-title" value="${esc(g.title || '')}" required></div>
    <div class="form-row"><div class="form-group"><label>Target Amount</label><input type="number" id="f-target" value="${g.target_amount || ''}" step="any" min="0" required></div><div class="form-group"><label>Current Amount</label><input type="number" id="f-current" value="${g.current_amount || 0}" step="any" min="0"></div></div>
    <div class="form-row"><div class="form-group"><label>Currency</label><input type="text" id="f-goal-currency" value="${esc(g.currency || 'EUR')}" maxlength="5"></div><div class="form-group"><label>Target Date</label><input type="date" id="f-target-date" value="${g.target_date ? g.target_date.split('T')[0] : ''}" required></div></div>
    <div class="form-row">
      <div class="form-group"><label>Year</label><input type="number" id="f-year" value="${g.year || currentYear}" min="2020" max="2050"></div>
      <div class="form-group"><label>Quarter</label><select id="f-quarter"><option value="">--</option>${[1,2,3,4].map(q => `<option value="${q}" ${g.quarter === q ? 'selected' : ''}>Q${q}</option>`).join('')}</select></div>
    </div>
    <div class="form-row">
      <div class="form-group"><label>Month</label><select id="f-month"><option value="">--</option>${[1,2,3,4,5,6,7,8,9,10,11,12].map(m => `<option value="${m}" ${g.month === m ? 'selected' : ''}>${monthNames[m]}</option>`).join('')}</select></div>
      <div class="form-group"><label>Week</label><select id="f-week"><option value="">--</option>${[1,2,3,4].map(w => `<option value="${w}" ${g.week === w ? 'selected' : ''}>Week ${w}</option>`).join('')}</select></div>
    </div>
    <div class="form-group"><label>Status</label><select id="f-status">${['not_started','in_progress','completed','on_hold','cancelled'].map(s => `<option value="${s}" ${(g.status || 'not_started') === s ? 'selected' : ''}>${s.replace(/_/g, ' ')}</option>`).join('')}</select></div>
    ${tags.length ? `<div class="form-group"><label>Tags</label><div class="tag-checkboxes" id="f-tags">${tagCheckboxes}</div></div>` : ''}
    <div class="form-group"><label>Description</label><textarea id="f-goal-desc" rows="2">${esc(g.description || '')}</textarea></div>
    ${g.id ? `<div class="form-group"><label style="display:flex;align-items:center;gap:0.5rem;cursor:pointer;"><input type="checkbox" id="f-achieved" ${g.achieved ? 'checked' : ''} style="width:auto;"> Mark as achieved</label></div>` : ''}
    <button type="submit" class="btn btn-primary btn-block"><i class="fas fa-save"></i> Save</button></form>`;
}

export function getGoalFormData() {
  const data = {
    title: document.getElementById('f-title').value,
    target_amount: parseFloat(document.getElementById('f-target').value),
    current_amount: parseFloat(document.getElementById('f-current').value) || 0,
    currency: document.getElementById('f-goal-currency').value.toUpperCase(),
    target_date: document.getElementById('f-target-date').value,
    description: document.getElementById('f-goal-desc').value,
    year: parseInt(document.getElementById('f-year').value) || null,
    quarter: parseInt(document.getElementById('f-quarter').value) || null,
    month: parseInt(document.getElementById('f-month').value) || null,
    week: parseInt(document.getElementById('f-week').value) || null,
    status: document.getElementById('f-status').value
  };

  const tagCheckboxes = document.querySelectorAll('#f-tags input[type="checkbox"]:checked');
  if (tagCheckboxes.length || document.getElementById('f-tags')) {
    data.tag_ids = Array.from(tagCheckboxes).map(cb => parseInt(cb.value));
  }

  const achievedEl = document.getElementById('f-achieved');
  if (achievedEl) data.achieved = achievedEl.checked;
  return data;
}

// ===== Tag Form =====
export function tagFormHtml(t = {}) {
  return `<form id="modal-form">
    <div class="form-group"><label>Name</label><input type="text" id="f-tag-name" value="${esc(t.name || '')}" required></div>
    <div class="form-group"><label>Color</label><input type="color" id="f-tag-color" value="${t.color || '#6366f1'}"></div>
    <button type="submit" class="btn btn-primary btn-block"><i class="fas fa-save"></i> Save</button></form>`;
}

export function getTagFormData() {
  return {
    name: document.getElementById('f-tag-name').value,
    color: document.getElementById('f-tag-color').value
  };
}

// ===== Rate Form =====
export function rateFormHtml(r = {}) {
  return `<form id="modal-form"><div class="form-row"><div class="form-group"><label>From Currency</label><input type="text" id="f-from" value="${esc(r.from_currency || '')}" required maxlength="5" placeholder="USD"></div><div class="form-group"><label>To Currency</label><input type="text" id="f-to" value="${esc(r.to_currency || '')}" required maxlength="5" placeholder="EUR"></div></div><div class="form-group"><label>Rate</label><input type="number" id="f-rate" value="${r.rate || ''}" step="any" min="0" required></div><button type="submit" class="btn btn-primary btn-block"><i class="fas fa-save"></i> Save</button></form>`;
}

export function getRateFormData() {
  return {
    from_currency: document.getElementById('f-from').value.toUpperCase(),
    to_currency: document.getElementById('f-to').value.toUpperCase(),
    rate: parseFloat(document.getElementById('f-rate').value)
  };
}

// ===== Symbol Search =====
export function initSymbolSearch(inputId, suggestionsId) {
  const input = document.getElementById(inputId);
  const suggestions = document.getElementById(suggestionsId);
  if (!input || !suggestions) return;
  let debounceTimer;
  input.addEventListener('input', () => {
    clearTimeout(debounceTimer);
    const query = input.value.trim();
    if (query.length < 1) { suggestions.classList.add('hidden'); return; }
    debounceTimer = setTimeout(async () => {
      try {
        const results = await API.get(`/api/prices/search/${encodeURIComponent(query)}`);
        if (results?.length) {
          suggestions.innerHTML = results.slice(0, 10).map(r =>
            `<div class="symbol-suggestion" data-symbol="${esc(r.symbol)}"><div><strong>${esc(r.symbol)}</strong> <span class="sym-name">${esc(r.name)}</span></div><span class="sym-type">${esc(r.type || '')}</span></div>`
          ).join('');
          suggestions.querySelectorAll('.symbol-suggestion').forEach(item => {
            item.addEventListener('click', () => { input.value = item.dataset.symbol; suggestions.classList.add('hidden'); });
          });
          suggestions.classList.remove('hidden');
        } else suggestions.classList.add('hidden');
      } catch { suggestions.classList.add('hidden'); }
    }, 250);
  });
  input.addEventListener('focus', () => { if (suggestions.children.length > 0 && input.value.trim().length >= 1) suggestions.classList.remove('hidden'); });
  document.addEventListener('click', (e) => { if (!suggestions.contains(e.target) && e.target !== input) suggestions.classList.add('hidden'); });
}
