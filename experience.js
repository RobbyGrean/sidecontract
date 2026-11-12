(() => {
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const systemTheme = matchMedia('(prefers-color-scheme: dark)');
  const root = document.documentElement;
  const toggle = document.querySelector('.theme-toggle');
  let manualTheme = false;
  try { manualTheme = ['dark', 'light'].includes(localStorage.getItem('sidecontract-theme')); } catch {}
  function updateToggle() {
    const dark = root.dataset.theme === 'dark';
    toggle.setAttribute('aria-pressed', String(dark));
    toggle.setAttribute('aria-label', dark ? 'เปิดโหมดสว่าง' : 'เปิดโหมดมืด');
    toggle.querySelector('span').textContent = dark ? 'โหมดสว่าง' : 'โหมดมืด';
  }
  if (toggle) {
    toggle.hidden = false;
    updateToggle();
    toggle.addEventListener('click', () => {
      root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
      manualTheme = true;
      try { localStorage.setItem('sidecontract-theme', root.dataset.theme); } catch {}
      updateToggle();
    });
    systemTheme.addEventListener('change', event => {
      if (!manualTheme) { root.dataset.theme = event.matches ? 'dark' : 'light'; updateToggle(); }
    });
  }
  const stars = document.createElement('div');
  stars.className = 'star-field';
  stars.setAttribute('aria-hidden', 'true');
  for (let i = 0; i < 42; i++) {
    const star = document.createElement('i');
    star.style.cssText = `left:${(i * 37.7) % 100}%;top:${(i * 23.3) % 100}%;--delay:-${i % 7}s;--duration:${4 + i % 5}s;--size:${i % 6 === 0 ? 4 : 2}px`;
    stars.append(star);
  }
  document.body.prepend(stars);
  function visibility() { stars.classList.toggle('paused', document.hidden); }
  document.addEventListener('visibilitychange', visibility);
  visibility();

  const carousel = document.querySelector('.hero-carousel');
  if (!carousel) return;
  const slides = [...carousel.querySelectorAll('.hero-slide')];
  if (slides.length < 2) {
    const controls = carousel.querySelector('.carousel-controls');
    if (controls) controls.hidden = true;
    return;
  }
  const dots = [...carousel.querySelectorAll('[data-slide]')];
  const caption = carousel.querySelector('figcaption');
  const pause = carousel.querySelector('[data-carousel="pause"]');
  let current = 0, timer, paused = false, hovered = false, focused = false;
  function render() {
    slides.forEach((slide, index) => { slide.hidden = index !== current; });
    dots.forEach((dot, index) => dot.setAttribute('aria-pressed', String(index === current)));
    caption.textContent = slides[current].dataset.caption;
    carousel.querySelector('#slide-count').textContent = `${current + 1} / ${slides.length}`;
  }
  function schedule() {
    clearInterval(timer);
    const running = !paused && !hovered && !focused && !document.hidden;
    caption.setAttribute('aria-live', running ? 'off' : 'polite');
    pause.textContent = paused ? 'เล่น' : 'หยุด';
    pause.setAttribute('aria-label', paused ? 'เริ่มหมุนภาพอัตโนมัติ' : 'หยุดหมุนภาพอัตโนมัติ');
    if (running) timer = setInterval(() => { current = (current + 1) % slides.length; render(); }, 4000);
  }
  function select(index) { current = (index + slides.length) % slides.length; paused = true; render(); schedule(); }
  carousel.querySelector('[data-carousel="prev"]').addEventListener('click', () => select(current - 1));
  carousel.querySelector('[data-carousel="next"]').addEventListener('click', () => select(current + 1));
  dots.forEach((dot, index) => dot.addEventListener('click', () => select(index)));
  pause.addEventListener('click', () => { paused = !paused; schedule(); });
  carousel.addEventListener('pointerenter', event => { if (event.pointerType !== 'touch') { hovered = true; schedule(); } });
  carousel.addEventListener('pointerleave', () => { hovered = false; schedule(); });
  carousel.addEventListener('focusin', () => { focused = true; schedule(); });
  carousel.addEventListener('focusout', event => { if (!carousel.contains(event.relatedTarget)) { focused = false; schedule(); } });
  carousel.addEventListener('keydown', event => {
    if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
      event.preventDefault(); select(current + (event.key === 'ArrowRight' ? 1 : -1));
    }
  });
  reduced.addEventListener('change', () => { if (reduced.matches) paused = true; schedule(); });
  document.addEventListener('visibilitychange', schedule);
  carousel.querySelector('.carousel-controls').hidden = false;
  render(); schedule();
})();
