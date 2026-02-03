/* ===== Theme Module ===== */

export function initTheme() {
  const saved = localStorage.getItem('wealth-theme') || 'dark';
  document.documentElement.setAttribute('data-theme', saved);
  updateThemeButtons(saved);
}

export function setTheme(theme) {
  document.documentElement.setAttribute('data-theme', theme);
  localStorage.setItem('wealth-theme', theme);
  updateThemeButtons(theme);
  // Dispatch event for chart redraws
  window.dispatchEvent(new CustomEvent('theme:changed', { detail: { theme } }));
}

export function updateThemeButtons(theme) {
  document.querySelectorAll('.theme-option').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.themeVal === theme);
  });
}

export function getCurrentTheme() {
  return document.documentElement.getAttribute('data-theme') || 'dark';
}
