/* ===== Modal Module ===== */

export function openModal(title, bodyHtml) {
  document.getElementById('modal-title').textContent = title;
  document.getElementById('modal-body').innerHTML = bodyHtml;
  document.getElementById('modal-overlay').classList.remove('hidden');
}

export function closeModal() {
  document.getElementById('modal-overlay').classList.add('hidden');
}

export function openImportModal() {
  document.getElementById('import-modal-overlay').classList.remove('hidden');
  document.getElementById('import-result').classList.add('hidden');
  document.getElementById('import-format-detected').classList.add('hidden');
  document.getElementById('import-file').value = '';
}

export function closeImportModal() {
  document.getElementById('import-modal-overlay').classList.add('hidden');
}

export function initModalListeners() {
  document.querySelectorAll('.modal-close').forEach(btn => btn.addEventListener('click', closeModal));
  document.getElementById('modal-overlay').addEventListener('click', (e) => {
    if (e.target === e.currentTarget) closeModal();
  });
  document.querySelectorAll('.import-modal-close').forEach(btn => btn.addEventListener('click', closeImportModal));
  document.getElementById('import-modal-overlay').addEventListener('click', (e) => {
    if (e.target === e.currentTarget) closeImportModal();
  });
}
