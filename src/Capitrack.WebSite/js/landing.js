// Capitrack landing — vanilla interactions

// Copy install command
(function () {
  var btn = document.getElementById('copy-install');
  var cmd = document.getElementById('install-cmd');
  if (btn && cmd) {
    btn.addEventListener('click', function () {
      var text = cmd.textContent.trim();
      navigator.clipboard && navigator.clipboard.writeText(text);
      var orig = btn.innerHTML;
      btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg> Copied';
      setTimeout(function () { btn.innerHTML = orig; }, 1600);
    });
  }
})();

// Hero browser screenshot tabs
(function () {
  var tabs = document.getElementById('shot-tabs');
  var stack = document.getElementById('shot-stack');
  if (!tabs || !stack) return;
  tabs.addEventListener('click', function (e) {
    var b = e.target.closest('button[data-shot]');
    if (!b) return;
    var idx = b.dataset.shot;
    tabs.querySelectorAll('button').forEach(function (x) { x.classList.toggle('active', x === b); });
    stack.querySelectorAll('img').forEach(function (img) { img.classList.toggle('active', img.dataset.shot === idx); });
  });
})();

// Quick-start terminal tabs
(function () {
  var tabs = document.getElementById('qs-tabs');
  if (!tabs) return;
  var panes = document.querySelectorAll('.terminal-pane');
  tabs.addEventListener('click', function (e) {
    var b = e.target.closest('button[data-qs]');
    if (!b) return;
    var key = b.dataset.qs;
    tabs.querySelectorAll('.qs-tab').forEach(function (x) { x.classList.toggle('active', x === b); });
    panes.forEach(function (p) { p.classList.toggle('active', p.dataset.qs === key); });
  });
})();

// Active nav link on scroll
(function () {
  var links = Array.prototype.slice.call(document.querySelectorAll('.nav-link[href^="#"]'));
  if (!links.length || !('IntersectionObserver' in window)) return;
  var map = {};
  links.forEach(function (l) {
    var id = l.getAttribute('href').slice(1);
    var sec = document.getElementById(id);
    if (sec) map[id] = l;
  });
  var obs = new IntersectionObserver(function (entries) {
    entries.forEach(function (en) {
      if (en.isIntersecting) {
        links.forEach(function (l) { l.classList.remove('active'); });
        if (map[en.target.id]) map[en.target.id].classList.add('active');
      }
    });
  }, { rootMargin: '-45% 0px -50% 0px' });
  Object.keys(map).forEach(function (id) { obs.observe(document.getElementById(id)); });
})();
