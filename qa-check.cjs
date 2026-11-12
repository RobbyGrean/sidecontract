const { chromium } = require('C:/Users/MSI/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('fs');
(async()=>{
const browser=await chromium.launch({headless:true,channel:"chrome"});
const page=await browser.newPage(); const errors=[];page.on('pageerror',e=>errors.push(e.message));
fs.mkdirSync('qa',{recursive:true});
for(const [name,width,height] of [['desktop',1366,768],['mobile',390,844]]){
await page.setViewportSize({width,height});await page.goto('file:///H:/CodexProjects/346/index.html');await page.evaluate(()=>document.fonts.ready);
await page.screenshot({path:`qa/${name}.png`,fullPage:true});
console.log(name,await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth,downloadBottom:document.querySelector('.hero .primary').getBoundingClientRect().bottom,guideLinks:[...document.querySelectorAll('[data-link^="guide"]')].map(e=>({href:e.getAttribute('href'),disabled:e.getAttribute('aria-disabled')})),downloads:[...document.querySelectorAll('[data-link="installerUrl"],[data-link="sourceUrl"]')].map(e=>e.getAttribute('href'))})));
}
await page.emulateMedia({reducedMotion:'reduce'});console.log('reducedMotion',await page.locator('.primary').first().evaluate(e=>getComputedStyle(e,'::before').animationName));
await page.keyboard.press('Tab');console.log('keyboardFocus',await page.evaluate(()=>document.activeElement.textContent));
console.log('pageErrors',errors);await browser.close();
})();
