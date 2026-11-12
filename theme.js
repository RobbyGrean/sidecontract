// Apply the saved theme before the page paints; storage may be blocked.
(() => {
  let theme;
  try { theme = localStorage.getItem('sidecontract-theme'); } catch {}
  document.documentElement.dataset.theme = theme === 'dark' || theme === 'light'
    ? theme : matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
})();

