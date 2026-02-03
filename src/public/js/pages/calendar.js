/* ===== Calendar Page ===== */

import { API } from '../modules/api.js';
import { formatMoney, formatDate, esc } from '../modules/utils.js';

let calendarState = {
  view: 'month', // month, week, year
  currentDate: new Date(),
  transactions: [],
  dailyWealth: {} // date -> { total_wealth, total_cost }
};

export async function loadCalendar() {
  const container = document.getElementById('calendar-container');
  container.innerHTML = '<div class="loading-spinner">Loading calendar...</div>';

  try {
    const transactions = await API.get('/api/transactions?limit=5000');
    calendarState.transactions = transactions || [];

    // Also save today's wealth snapshot
    API.post('/api/prices/daily-wealth', {}).catch(() => {});

    // Load daily wealth for the visible range
    await loadDailyWealthForView();
    renderCalendar();
  } catch (e) {
    console.error('Calendar error:', e);
    container.innerHTML = '<div class="empty-state"><i class="fas fa-calendar-times"></i><p>Failed to load calendar.</p></div>';
  }
}

async function loadDailyWealthForView() {
  try {
    const d = calendarState.currentDate;
    let start, end;

    if (calendarState.view === 'month') {
      start = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
      const lastDay = new Date(d.getFullYear(), d.getMonth() + 1, 0);
      end = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(lastDay.getDate()).padStart(2, '0')}`;
    } else if (calendarState.view === 'week') {
      const weekStart = getWeekStart(d);
      const weekEnd = new Date(weekStart);
      weekEnd.setDate(weekEnd.getDate() + 6);
      start = toDateStr(weekStart);
      end = toDateStr(weekEnd);
    } else {
      start = `${d.getFullYear()}-01-01`;
      end = `${d.getFullYear()}-12-31`;
    }

    const data = await API.get(`/api/prices/daily-wealth?start=${start}&end=${end}`);
    calendarState.dailyWealth = {};
    for (const row of (data || [])) {
      calendarState.dailyWealth[row.date] = row;
    }
  } catch (e) {
    console.error('Daily wealth load error:', e);
  }
}

function toDateStr(d) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function calendarPrev() {
  const d = calendarState.currentDate;
  if (calendarState.view === 'month') d.setMonth(d.getMonth() - 1);
  else if (calendarState.view === 'week') d.setDate(d.getDate() - 7);
  else if (calendarState.view === 'year') d.setFullYear(d.getFullYear() - 1);
  loadDailyWealthForView().then(() => renderCalendar());
}

export function calendarNext() {
  const d = calendarState.currentDate;
  if (calendarState.view === 'month') d.setMonth(d.getMonth() + 1);
  else if (calendarState.view === 'week') d.setDate(d.getDate() + 7);
  else if (calendarState.view === 'year') d.setFullYear(d.getFullYear() + 1);
  loadDailyWealthForView().then(() => renderCalendar());
}

export function calendarToday() {
  calendarState.currentDate = new Date();
  loadDailyWealthForView().then(() => renderCalendar());
}

export function setCalendarView(view) {
  calendarState.view = view;
  loadDailyWealthForView().then(() => renderCalendar());
}

function renderCalendar() {
  updateTitle();
  switch (calendarState.view) {
    case 'month': renderMonthView(); break;
    case 'week': renderWeekView(); break;
    case 'year': renderYearView(); break;
  }
}

function updateTitle() {
  const d = calendarState.currentDate;
  const months = ['January','February','March','April','May','June','July','August','September','October','November','December'];
  let title = '';
  if (calendarState.view === 'month') {
    title = `${months[d.getMonth()]} ${d.getFullYear()}`;
  } else if (calendarState.view === 'week') {
    const start = getWeekStart(d);
    const end = new Date(start);
    end.setDate(end.getDate() + 6);
    const fmt = (dt) => `${months[dt.getMonth()].slice(0,3)} ${dt.getDate()}`;
    title = `${fmt(start)} - ${fmt(end)}, ${end.getFullYear()}`;
  } else {
    title = `${d.getFullYear()}`;
  }
  document.getElementById('cal-title').textContent = title;
}

function getWeekStart(date) {
  const d = new Date(date);
  const day = d.getDay();
  const diff = d.getDate() - day + (day === 0 ? -6 : 1); // Monday start
  return new Date(d.setDate(diff));
}

function getTransactionsForDate(dateStr) {
  return calendarState.transactions.filter(tx => tx.date && tx.date.substring(0, 10) === dateStr);
}

function getTransactionsForMonth(year, month) {
  const prefix = `${year}-${String(month + 1).padStart(2, '0')}`;
  return calendarState.transactions.filter(tx => tx.date && tx.date.startsWith(prefix));
}

function txBadgeClass(type) {
  const map = { buy: 'badge-buy', sell: 'badge-sell', transfer_in: 'badge-transfer_in', transfer_out: 'badge-transfer_out', dividend: 'badge-dividend', interest: 'badge-interest', fee: 'badge-fee' };
  return map[type] || 'badge-buy';
}

function formatNumber(n) {
  if (n === 0) return '0';
  if (Math.abs(n) < 0.01) return n.toFixed(8);
  if (Math.abs(n) < 1) return n.toFixed(4);
  if (Math.abs(n) < 100) return n.toFixed(2);
  return n.toFixed(0);
}

function renderMonthView() {
  const container = document.getElementById('calendar-container');
  const d = calendarState.currentDate;
  const year = d.getFullYear();
  const month = d.getMonth();
  const firstDay = new Date(year, month, 1);
  const lastDay = new Date(year, month + 1, 0);
  const startDow = firstDay.getDay() === 0 ? 6 : firstDay.getDay() - 1; // Monday = 0

  const today = new Date();
  const todayStr = toDateStr(today);

  const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  let html = '<div class="cal-month-grid">';
  html += '<div class="cal-month-header">';
  for (const day of days) html += `<div class="cal-day-name">${day}</div>`;
  html += '</div>';
  html += '<div class="cal-month-body">';

  // Previous month filler
  const prevMonth = new Date(year, month, 0);
  for (let i = startDow - 1; i >= 0; i--) {
    const dayNum = prevMonth.getDate() - i;
    html += `<div class="cal-day cal-day-outside"><span class="cal-day-num">${dayNum}</span></div>`;
  }

  // Current month days
  for (let day = 1; day <= lastDay.getDate(); day++) {
    const dateStr = `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    const dayTx = getTransactionsForDate(dateStr);
    const isToday = dateStr === todayStr;
    const wealth = calendarState.dailyWealth[dateStr];

    html += `<div class="cal-day${isToday ? ' cal-day-today' : ''}${dayTx.length ? ' cal-day-has-tx' : ''}">`;
    html += `<span class="cal-day-num">${day}</span>`;

    // Show daily wealth if available
    if (wealth) {
      const gain = wealth.total_wealth - wealth.total_cost;
      const gainClass = gain >= 0 ? 'cal-wealth-positive' : 'cal-wealth-negative';
      html += `<div class="cal-day-wealth ${gainClass}" title="Total wealth: ${formatMoney(wealth.total_wealth, wealth.base_currency || 'EUR')}">`;
      html += `<span class="cal-wealth-amount">${formatMoneyCompact(wealth.total_wealth)}</span>`;
      html += `</div>`;
    }

    if (dayTx.length > 0) {
      html += '<div class="cal-day-events">';
      const shown = dayTx.slice(0, 2);
      for (const tx of shown) {
        const total = (tx.quantity || 0) * (tx.price || 0);
        html += `<div class="cal-event ${txBadgeClass(tx.type)}" title="${esc(tx.symbol)} - ${tx.type} - ${formatMoney(total, tx.currency)}">`;
        html += `<span class="cal-event-symbol">${esc(tx.symbol)}</span>`;
        html += `<span class="cal-event-type">${tx.type}</span>`;
        html += `<span class="cal-event-amount">${formatMoneyCompact(total)}</span>`;
        html += '</div>';
      }
      if (dayTx.length > 2) {
        html += `<div class="cal-event-more">+${dayTx.length - 2} more</div>`;
      }
      html += '</div>';
    }

    html += '</div>';
  }

  // Next month filler
  const totalCells = startDow + lastDay.getDate();
  const remaining = totalCells % 7 === 0 ? 0 : 7 - (totalCells % 7);
  for (let i = 1; i <= remaining; i++) {
    html += `<div class="cal-day cal-day-outside"><span class="cal-day-num">${i}</span></div>`;
  }

  html += '</div></div>';
  container.innerHTML = html;
}

function renderWeekView() {
  const container = document.getElementById('calendar-container');
  const weekStart = getWeekStart(new Date(calendarState.currentDate));
  const today = new Date();
  const todayStr = toDateStr(today);

  const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

  let html = '<div class="cal-week-grid">';
  for (let i = 0; i < 7; i++) {
    const d = new Date(weekStart);
    d.setDate(d.getDate() + i);
    const dateStr = toDateStr(d);
    const dayTx = getTransactionsForDate(dateStr);
    const isToday = dateStr === todayStr;
    const wealth = calendarState.dailyWealth[dateStr];

    html += `<div class="cal-week-day${isToday ? ' cal-day-today' : ''}">`;
    html += `<div class="cal-week-day-header">`;
    html += `<span class="cal-week-day-name">${days[i]}</span>`;
    html += `<span class="cal-week-day-date">${months[d.getMonth()]} ${d.getDate()}</span>`;

    // Show daily wealth in week header
    if (wealth) {
      const gain = wealth.total_wealth - wealth.total_cost;
      const gainPct = wealth.total_cost > 0 ? ((gain / wealth.total_cost) * 100) : 0;
      const gainClass = gain >= 0 ? 'cal-wealth-positive' : 'cal-wealth-negative';
      html += `<div class="cal-week-wealth ${gainClass}">`;
      html += `<span class="cal-wealth-label">Wealth</span>`;
      html += `<span class="cal-wealth-value">${formatMoney(wealth.total_wealth, wealth.base_currency || 'EUR')}</span>`;
      html += `<span class="cal-wealth-change">${gain >= 0 ? '+' : ''}${gainPct.toFixed(1)}%</span>`;
      html += `</div>`;
    }

    html += '</div>';
    html += '<div class="cal-week-day-events">';

    if (dayTx.length === 0) {
      html += '<div class="cal-week-empty">No transactions</div>';
    } else {
      // Calculate daily summary
      let dayBuyTotal = 0, daySellTotal = 0;
      for (const tx of dayTx) {
        const total = (tx.quantity || 0) * (tx.price || 0);
        if (['buy', 'transfer_in'].includes(tx.type)) dayBuyTotal += total;
        if (['sell', 'transfer_out'].includes(tx.type)) daySellTotal += total;
      }

      // Show summary bar
      if (dayBuyTotal > 0 || daySellTotal > 0) {
        html += '<div class="cal-week-summary">';
        if (dayBuyTotal > 0) html += `<span class="cal-summary-buy"><i class="fas fa-arrow-down"></i> ${formatMoneyCompact(dayBuyTotal)}</span>`;
        if (daySellTotal > 0) html += `<span class="cal-summary-sell"><i class="fas fa-arrow-up"></i> ${formatMoneyCompact(daySellTotal)}</span>`;
        html += '</div>';
      }

      for (const tx of dayTx) {
        const total = (tx.quantity || 0) * (tx.price || 0);
        html += `<div class="cal-week-event ${txBadgeClass(tx.type)}">`;
        html += `<div class="cal-week-event-header">`;
        html += `<span class="cal-week-event-symbol">${esc(tx.symbol)}</span>`;
        html += `<span class="badge ${txBadgeClass(tx.type)}">${tx.type.replace('_', ' ')}</span>`;
        html += `</div>`;
        html += `<div class="cal-week-event-detail">`;
        html += `<span>${tx.quantity ? formatNumber(tx.quantity) + ' @ ' + formatMoney(tx.price, tx.currency) : ''}</span>`;
        html += `<span class="cal-week-event-total">${formatMoney(total, tx.currency)}</span>`;
        html += `</div>`;
        if (tx.fee > 0) {
          html += `<div class="cal-week-event-fee"><span>Fee: ${formatMoney(tx.fee, tx.currency)}</span></div>`;
        }
        if (tx.notes) {
          html += `<div class="cal-week-event-notes">${esc(tx.notes.substring(0, 50))}${tx.notes.length > 50 ? '...' : ''}</div>`;
        }
        html += '</div>';
      }
    }

    html += '</div></div>';
  }
  html += '</div>';
  container.innerHTML = html;
}

function renderYearView() {
  const container = document.getElementById('calendar-container');
  const year = calendarState.currentDate.getFullYear();
  const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
  const today = new Date();

  let html = '<div class="cal-year-grid">';
  for (let m = 0; m < 12; m++) {
    const monthTx = getTransactionsForMonth(year, m);
    const isCurrentMonth = today.getFullYear() === year && today.getMonth() === m;

    // Group by type and calculate totals
    const typeCounts = {};
    let totalBuy = 0, totalSell = 0, totalValue = 0;
    for (const tx of monthTx) {
      typeCounts[tx.type] = (typeCounts[tx.type] || 0) + 1;
      const txTotal = (tx.quantity || 0) * (tx.price || 0);
      totalValue += txTotal;
      if (['buy', 'transfer_in'].includes(tx.type)) totalBuy += txTotal;
      if (['sell', 'transfer_out'].includes(tx.type)) totalSell += txTotal;
    }

    // Get end-of-month wealth if available
    const lastDayOfMonth = new Date(year, m + 1, 0).getDate();
    const endOfMonthStr = `${year}-${String(m + 1).padStart(2, '0')}-${String(lastDayOfMonth).padStart(2, '0')}`;
    const monthWealth = calendarState.dailyWealth[endOfMonthStr];

    html += `<div class="cal-year-month${isCurrentMonth ? ' cal-year-month-current' : ''}${monthTx.length ? ' cal-year-month-has-tx' : ''}">`;
    html += `<div class="cal-year-month-header">${months[m]}</div>`;
    html += `<div class="cal-year-month-count">${monthTx.length} transaction${monthTx.length !== 1 ? 's' : ''}</div>`;

    // Show wealth snapshot
    if (monthWealth) {
      const gain = monthWealth.total_wealth - monthWealth.total_cost;
      const gainClass = gain >= 0 ? 'cal-wealth-positive' : 'cal-wealth-negative';
      html += `<div class="cal-year-month-wealth ${gainClass}">`;
      html += `<span>${formatMoneyCompact(monthWealth.total_wealth)}</span>`;
      html += `</div>`;
    }

    if (monthTx.length > 0) {
      // Show buy/sell summary
      html += '<div class="cal-year-month-summary">';
      if (totalBuy > 0) html += `<span class="cal-summary-buy"><i class="fas fa-arrow-down"></i> ${formatMoneyCompact(totalBuy)}</span>`;
      if (totalSell > 0) html += `<span class="cal-summary-sell"><i class="fas fa-arrow-up"></i> ${formatMoneyCompact(totalSell)}</span>`;
      html += '</div>';

      html += '<div class="cal-year-month-types">';
      for (const [type, count] of Object.entries(typeCounts)) {
        html += `<span class="badge ${txBadgeClass(type)}">${type} (${count})</span>`;
      }
      html += '</div>';

      // Mini heatmap
      const daysInMonth = new Date(year, m + 1, 0).getDate();
      html += '<div class="cal-year-mini-grid">';
      for (let d = 1; d <= daysInMonth; d++) {
        const dateStr = `${year}-${String(m + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
        const dayTx = getTransactionsForDate(dateStr);
        const intensity = dayTx.length === 0 ? 0 : Math.min(dayTx.length, 4);
        html += `<div class="cal-mini-day cal-heat-${intensity}" title="${dateStr}: ${dayTx.length} tx"></div>`;
      }
      html += '</div>';
    }

    html += '</div>';
  }
  html += '</div>';
  container.innerHTML = html;
}

function formatMoneyCompact(amount) {
  if (amount === undefined || amount === null) return '--';
  if (Math.abs(amount) >= 1e6) return (amount / 1e6).toFixed(1) + 'M';
  if (Math.abs(amount) >= 1e3) return (amount / 1e3).toFixed(1) + 'K';
  return amount.toFixed(0);
}
