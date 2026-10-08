'use strict';
(() => {
    const toggle = document.getElementById('theme-toggle');
    const applyTheme = theme => {
        document.documentElement.dataset.bsTheme = theme;
        toggle.textContent = theme === 'dark' ? 'Light theme' : 'Dark theme';
        toggle.setAttribute('aria-label', theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme');
        document.dispatchEvent(new CustomEvent('serveflow:theme', { detail: theme }));
    };
    let savedTheme; try { savedTheme = localStorage.getItem('serveflow-theme'); } catch { }
    applyTheme(savedTheme === 'dark' || savedTheme === 'light' ? savedTheme : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    toggle.addEventListener('click', () => { const theme = document.documentElement.dataset.bsTheme === 'dark' ? 'light' : 'dark'; applyTheme(theme); try { localStorage.setItem('serveflow-theme', theme); } catch { } });
    const sidebar = document.getElementById('staff-sidebar'); const button = document.getElementById('sidebar-toggle'); const backdrop = document.getElementById('sidebar-backdrop');
    if (!sidebar || !button) return;
    const mobile = matchMedia('(max-width: 991px)'); const main = document.querySelector('.workspace-main');
    function open(value, restore = false) { document.body.classList.toggle('sidebar-open', value); button.setAttribute('aria-expanded', String(value)); backdrop.hidden = !value; sidebar.inert = mobile.matches && !value; main.inert = mobile.matches && value; sidebar.setAttribute('role', mobile.matches && value ? 'dialog' : 'complementary'); if (mobile.matches && value) sidebar.setAttribute('aria-modal', 'true'); else sidebar.removeAttribute('aria-modal'); if (value) sidebar.querySelector('a').focus(); else if (restore) button.focus(); }
    button.addEventListener('click', () => open(!document.body.classList.contains('sidebar-open'), true)); backdrop.addEventListener('click', () => open(false, true)); document.getElementById('sidebar-close').addEventListener('click', () => open(false, true));
    document.addEventListener('keydown', event => {
        if (!document.body.classList.contains('sidebar-open')) return;
        if (event.key === 'Escape') { event.preventDefault(); open(false, true); }
        if (event.key === 'Tab') { const items = [...sidebar.querySelectorAll('a,button')].filter(x => x.getClientRects().length); const first = items[0], last = items[items.length - 1]; if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); } else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); } }
    });
    mobile.addEventListener('change', () => open(false)); open(false);
})();
