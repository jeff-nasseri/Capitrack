/* ===== Chart Component ===== */

import { state, getSetting } from '../modules/state.js';
import { formatMoney, formatDate } from '../modules/utils.js';

/**
 * Render a line chart with optional transaction dots.
 * Shows date on X-axis and price on Y-axis.
 * @param {string} canvasId - Canvas element ID
 * @param {Array} data - Array of { date, value/close }
 * @param {string} stateKey - Key in state to store chart instance
 * @param {number} height - Chart height
 * @param {Array} [transactions] - Optional transactions to show as dots
 */
export function renderLineChart(canvasId, data, stateKey, height, transactions = []) {
  const canvas = document.getElementById(canvasId);
  if (!canvas) return;
  if (state[stateKey]) { state[stateKey].destroy(); state[stateKey] = null; }
  if (!data.length) return;

  const ctx = canvas.getContext('2d');
  const s = getComputedStyle(document.documentElement);

  // Create gradient
  const gradient = ctx.createLinearGradient(0, 0, 0, height);
  gradient.addColorStop(0, s.getPropertyValue('--chart-gradient-start').trim());
  gradient.addColorStop(1, s.getPropertyValue('--chart-gradient-end').trim());

  const labels = data.map(d => {
    const dt = new Date(d.date);
    return dt.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  });
  const values = data.map(d => d.value !== undefined ? d.value : d.close);

  // Calculate smart Y-axis bounds
  const minVal = Math.min(...values);
  const maxVal = Math.max(...values);
  const range = maxVal - minVal;
  const yPadding = range * 0.08 || 1;

  const datasets = [{
    data: values,
    borderColor: s.getPropertyValue('--chart-line').trim(),
    backgroundColor: gradient,
    fill: true,
    tension: 0.3,
    pointRadius: 0,
    pointHitRadius: 10,
    borderWidth: 1.5,
    order: 1
  }];

  // Add transaction dots if enabled and transactions provided
  const showDots = getSetting('showTransactionDots');
  if (showDots && transactions.length > 0) {
    const txPoints = buildTransactionPoints(data, transactions, values);
    if (txPoints.data.length > 0) {
      datasets.push({
        data: txPoints.data,
        borderColor: s.getPropertyValue('--chart-dot').trim(),
        backgroundColor: txPoints.colors,
        pointRadius: txPoints.radii,
        pointHoverRadius: 8,
        pointBorderWidth: 2,
        pointBorderColor: txPoints.borderColors,
        showLine: false,
        order: 0,
        // Store tx data for tooltip
        txData: txPoints.txInfo
      });
    }
  }

  // Format price for Y-axis based on magnitude
  function formatAxisPrice(value) {
    if (Math.abs(value) >= 1e6) return (value / 1e6).toFixed(1) + 'M';
    if (Math.abs(value) >= 1e3) return (value / 1e3).toFixed(1) + 'K';
    if (Math.abs(value) >= 100) return value.toFixed(0);
    if (Math.abs(value) >= 1) return value.toFixed(2);
    return value.toFixed(4);
  }

  // Determine how many X-axis labels to show (avoid clutter)
  const maxLabels = 8;

  state[stateKey] = new Chart(ctx, {
    type: 'line',
    data: { labels, datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { intersect: false, mode: 'index' },
      plugins: {
        legend: { display: false },
        tooltip: {
          backgroundColor: s.getPropertyValue('--bg-card').trim(),
          titleColor: s.getPropertyValue('--text-muted').trim(),
          bodyColor: s.getPropertyValue('--text').trim(),
          borderColor: s.getPropertyValue('--border').trim(),
          borderWidth: 1,
          displayColors: false,
          callbacks: {
            title: (items) => {
              if (!items.length) return '';
              const idx = items[0].dataIndex;
              if (idx < data.length) {
                const dt = new Date(data[idx].date);
                return dt.toLocaleDateString('en-US', { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' });
              }
              return items[0].label;
            },
            label: (context) => {
              // If this is the transaction dots dataset
              if (context.datasetIndex === 1 && context.dataset.txData) {
                const txInfo = context.dataset.txData[context.dataIndex];
                if (txInfo) {
                  return [
                    `${txInfo.type.toUpperCase()} ${txInfo.symbol}`,
                    `Qty: ${txInfo.quantity} @ ${formatMoney(txInfo.price, txInfo.currency)}`,
                    `Total: ${formatMoney(txInfo.total, txInfo.currency)}`,
                    `Date: ${formatDate(txInfo.date)}`
                  ];
                }
              }
              return formatMoney(context.parsed.y, 'USD');
            }
          }
        }
      },
      scales: {
        x: {
          display: true,
          grid: {
            display: false,
          },
          border: {
            display: false,
          },
          ticks: {
            color: s.getPropertyValue('--text-dim').trim(),
            font: { size: 10 },
            maxRotation: 0,
            autoSkip: true,
            maxTicksLimit: maxLabels,
          }
        },
        y: {
          display: true,
          position: 'right',
          grid: {
            color: s.getPropertyValue('--border-light').trim() || 'rgba(128,128,128,0.1)',
          },
          border: {
            display: false,
          },
          min: minVal - yPadding,
          max: maxVal + yPadding,
          ticks: {
            color: s.getPropertyValue('--text-dim').trim(),
            font: { size: 10 },
            maxTicksLimit: 5,
            callback: function(value) {
              return formatAxisPrice(value);
            }
          }
        }
      }
    }
  });
}

/**
 * Build scatter points for transactions overlaid on the chart.
 */
function buildTransactionPoints(chartData, transactions, chartValues) {
  const result = { data: [], colors: [], borderColors: [], radii: [], txInfo: [] };
  const s = getComputedStyle(document.documentElement);
  const successColor = s.getPropertyValue('--success').trim();
  const dangerColor = s.getPropertyValue('--danger').trim();
  const primaryColor = s.getPropertyValue('--primary').trim();

  for (const tx of transactions) {
    const txDate = tx.date.split('T')[0];
    // Find closest chart data point
    let closestIdx = -1;
    let closestDist = Infinity;
    for (let i = 0; i < chartData.length; i++) {
      const chartDate = chartData[i].date.split('T')[0];
      const dist = Math.abs(new Date(chartDate) - new Date(txDate));
      if (dist < closestDist) { closestDist = dist; closestIdx = i; }
    }
    if (closestIdx >= 0 && closestDist < 7 * 24 * 60 * 60 * 1000) { // within 7 days
      const isBuy = ['buy', 'transfer_in', 'dividend'].includes(tx.type);
      const color = isBuy ? successColor : dangerColor;
      result.data.push({ x: closestIdx, y: chartValues[closestIdx] });
      result.colors.push(color + '40');
      result.borderColors.push(color);
      result.radii.push(5);
      result.txInfo.push({
        type: tx.type,
        symbol: tx.symbol,
        quantity: tx.quantity,
        price: tx.price,
        currency: tx.currency || 'USD',
        total: tx.quantity * tx.price,
        date: tx.date
      });
    }
  }
  return result;
}
