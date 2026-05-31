// Capitrack docs — shared chrome injection + interactions

(function () {
  // ---- persisted theme (docs have a lightweight toggle) ----
  var savedTheme = localStorage.getItem('capitrack-doc-theme') || 'dark';
  var savedAccent = localStorage.getItem('capitrack-doc-accent') || 'indigo';
  document.documentElement.dataset.theme = savedTheme;
  document.documentElement.dataset.accent = savedAccent;

  var PAGES = [
    { group: 'Overview', items: [
      { id: 'getting-started', title: 'Getting started', href: 'getting-started.html',
        icon: '<path d="M5 12h14"/><path d="M12 5l7 7-7 7"/>' }
    ]},
    { group: 'Guides', items: [
      { id: 'configuration', title: 'Configuration', href: 'configuration.html',
        icon: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.9.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.9 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1A1.7 1.7 0 0 0 4.6 9a1.7 1.7 0 0 0-.3-1.9l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.9.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.9-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.9V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>' },
      { id: 'csv-import', title: 'CSV import', href: 'csv-import.html',
        icon: '<path d="M14 3v4a1 1 0 0 0 1 1h4"/><path d="M17 21H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h7l5 5v11a2 2 0 0 1-2 2z"/><path d="M9 15l3 3 3-3"/><path d="M12 18v-6"/>' }
    ]},
    { group: 'Reference', items: [
      { id: 'api-reference', title: 'API reference', href: 'api-reference.html',
        icon: '<path d="m16 18 6-6-6-6"/><path d="m8 6-6 6 6 6"/>' }
    ]}
  ];

  var current = document.body.dataset.doc;

  // ---------- Top nav ----------
  var nav = document.createElement('header');
  nav.className = 'nav';
  nav.innerHTML =
    '<div class="nav-inner">' +
      '<a href="../index.html" class="brand" aria-label="Capitrack home">' +
        '<span class="brand-mark"><svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M3 17l5-6 4 3 6-8"/><path d="M20 6v4h-4"/></svg></span>' +
        '<span class="brand-name"><b>Capi</b>track</span>' +
      '</a>' +
      '<span style="font-family:var(--mono);font-size:0.78rem;color:var(--text-dim);border:1px solid var(--border);padding:0.1rem 0.5rem;border-radius:6px;">docs</span>' +
      '<nav class="nav-links">' +
        '<a class="nav-link" href="../index.html#features">Features</a>' +
        '<a class="nav-link active" href="getting-started.html">Docs</a>' +
      '</nav>' +
      '<div class="nav-actions">' +
        '<button class="nav-gh" id="doc-theme-btn" type="button" aria-label="Toggle theme" style="padding:0.5rem;width:38px;justify-content:center;">' +
          '<svg id="theme-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"></svg>' +
        '</button>' +
        '<a class="nav-gh" href="https://github.com/jeff-nasseri/Capitrack" target="_blank" rel="noopener">' +
          '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.58 2 12.25c0 4.53 2.87 8.37 6.84 9.73.5.1.68-.22.68-.49 0-.24-.01-.88-.01-1.73-2.78.62-3.37-1.21-3.37-1.21-.45-1.18-1.11-1.49-1.11-1.49-.91-.63.07-.62.07-.62 1 .07 1.53 1.05 1.53 1.05.89 1.56 2.34 1.11 2.91.85.09-.66.35-1.11.63-1.37-2.22-.26-4.55-1.14-4.55-5.07 0-1.12.39-2.03 1.03-2.75-.1-.26-.45-1.3.1-2.71 0 0 .84-.27 2.75 1.05a9.4 9.4 0 0 1 5 0c1.91-1.32 2.75-1.05 2.75-1.05.55 1.41.2 2.45.1 2.71.64.72 1.03 1.63 1.03 2.75 0 3.94-2.34 4.81-4.57 5.06.36.32.68.94.68 1.9 0 1.37-.01 2.47-.01 2.81 0 .27.18.6.69.49A10.02 10.02 0 0 0 22 12.25C22 6.58 17.52 2 12 2z"/></svg>' +
          'GitHub' +
        '</a>' +
      '</div>' +
    '</div>';
  document.body.insertBefore(nav, document.body.firstChild);

  // theme toggle icon + handler
  var sun = '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>';
  var moon = '<path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/>';
  function paintThemeIcon() {
    var ic = document.getElementById('theme-icon');
    if (ic) ic.innerHTML = document.documentElement.dataset.theme === 'dark' ? sun : moon;
  }
  paintThemeIcon();
  var tbtn = document.getElementById('doc-theme-btn');
  if (tbtn) tbtn.addEventListener('click', function () {
    var next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    localStorage.setItem('capitrack-doc-theme', next);
    paintThemeIcon();
  });

  // ---------- Sidebar ----------
  var side = document.querySelector('.doc-side');
  if (side) {
    var html = '<div class="doc-search"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/></svg> Search docs <span class="kbd">⌘K</span></div>';
    PAGES.forEach(function (g) {
      html += '<div class="doc-nav-group"><h5>' + g.group + '</h5>';
      g.items.forEach(function (it) {
        var active = it.id === current ? ' class="active"' : '';
        html += '<a href="' + it.href + '"' + active + '>' +
          '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' + it.icon + '</svg>' +
          it.title + '</a>';
      });
      html += '</div>';
    });
    side.innerHTML = html;
  }

  // ---------- Code copy buttons ----------
  document.querySelectorAll('.code-block').forEach(function (block) {
    var btn = document.createElement('button');
    btn.className = 'code-copy';
    btn.type = 'button';
    btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>Copy';
    btn.addEventListener('click', function () {
      var code = block.querySelector('pre');
      var text = code ? code.innerText : '';
      navigator.clipboard && navigator.clipboard.writeText(text);
      btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>Copied';
      setTimeout(function () {
        btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>Copy';
      }, 1600);
    });
    block.appendChild(btn);
  });

  // ---------- Build right TOC + scrollspy ----------
  var toc = document.querySelector('.doc-toc');
  var content = document.querySelector('.doc-content');
  if (toc && content) {
    var heads = content.querySelectorAll('h2, h3');
    if (heads.length) {
      var t = '<h6>On this page</h6>';
      heads.forEach(function (h, i) {
        if (!h.id) h.id = 'h-' + i;
        var sub = h.tagName === 'H3' ? ' sub' : '';
        t += '<a class="toc-link' + sub + '" href="#' + h.id + '">' + h.textContent + '</a>';
      });
      toc.innerHTML = t;

      var tocLinks = toc.querySelectorAll('a');
      var spy = new IntersectionObserver(function (entries) {
        entries.forEach(function (en) {
          if (en.isIntersecting) {
            tocLinks.forEach(function (l) { l.classList.toggle('active', l.getAttribute('href') === '#' + en.target.id); });
          }
        });
      }, { rootMargin: '-80px 0px -70% 0px' });
      heads.forEach(function (h) { spy.observe(h); });
    } else {
      toc.style.display = 'none';
    }
  }

  // ---------- Mobile sidebar toggle ----------
  var menuBtn = document.querySelector('.doc-menu-btn');
  if (menuBtn && side) {
    menuBtn.addEventListener('click', function () { side.classList.toggle('open'); });
    side.addEventListener('click', function (e) { if (e.target.tagName === 'A') side.classList.remove('open'); });
  }
})();
