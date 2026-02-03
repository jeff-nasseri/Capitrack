/* ===== API Client Module ===== */

export const API = {
  async fetch(url, opts = {}) {
    const res = await fetch(url, {
      headers: { 'Content-Type': 'application/json', ...opts.headers },
      ...opts
    });
    if (res.status === 401 && !url.includes('/auth/')) {
      window.dispatchEvent(new CustomEvent('auth:unauthorized'));
      return null;
    }
    return res;
  },
  async get(url) { return (await this.fetch(url))?.json(); },
  async post(url, data) { return (await this.fetch(url, { method: 'POST', body: JSON.stringify(data) }))?.json(); },
  async put(url, data) { return (await this.fetch(url, { method: 'PUT', body: JSON.stringify(data) }))?.json(); },
  async del(url) { return (await this.fetch(url, { method: 'DELETE' }))?.json(); }
};
