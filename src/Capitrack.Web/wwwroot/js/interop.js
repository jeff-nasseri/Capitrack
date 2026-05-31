/* Small interop helpers for theme, localStorage, navigation, confirm dialogs and chart sizing. */
window.capitrack = {
    setTheme: function (t) {
        document.documentElement.setAttribute('data-theme', t);
        localStorage.setItem('wealth-theme', t);
    },
    getTheme: function () { return localStorage.getItem('wealth-theme') || 'dark'; },
    getLocal: function (k) { return localStorage.getItem(k); },
    setLocal: function (k, v) { localStorage.setItem(k, v); },
    go: function (url) { window.location.href = url; },
    reload: function () { window.location.reload(); },
    confirm: function (msg) { return window.confirm(msg); },
    cetClock: function () {
        return new Date().toLocaleString('en-GB', {
            timeZone: 'Europe/Berlin', weekday: 'short', year: 'numeric', month: 'short',
            day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit'
        }) + ' CET';
    },

    // Watch an element's width for responsive SVG charts. Returns the current width and
    // calls dotnetRef.OnResize(width) whenever it changes. Pass the same el to unobserveSize.
    observeSize: function (el, dotnetRef) {
        if (!el) return 0;
        try {
            const ro = new ResizeObserver(function () {
                dotnetRef.invokeMethodAsync('OnResize', Math.round(el.clientWidth));
            });
            ro.observe(el);
            el.__ctRo = ro;
        } catch (e) { /* ResizeObserver unsupported */ }
        return Math.round(el.clientWidth);
    },
    unobserveSize: function (el) {
        if (el && el.__ctRo) { el.__ctRo.disconnect(); el.__ctRo = null; }
    }
};
