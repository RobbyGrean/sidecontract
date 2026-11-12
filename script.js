// เปลี่ยนข้อมูลเว็บไซต์ทุกตำแหน่งจากที่นี่ ใช้ null สำหรับข้อมูลที่ยังไม่พร้อม
const SITE_CONFIG = {
  name: 'sidecontract',
  headline: 'กรอกข้อมูลให้พร้อม\nสร้างเอกสาร Word',
  description: 'โปรแกรม Windows แบบออฟไลน์ สำหรับกรอกข้อมูลและสร้างเอกสาร Word จากแม่แบบ ใช้กับงานจัดซื้อจัดจ้างและสัญญา หรืองานเบิกเงินเดือน โดยแยกข้อมูลเฉพาะงานและโฟลเดอร์ผลลัพธ์ เก็บทุกอย่างไว้ในเครื่อง',
  features: [
    { title: 'สอง workflow ในโปรแกรมเดียว', benefit: 'เลือกแท็บสีเขียวสำหรับจัดจ้างและสัญญา หรือสีฟ้าสำหรับเบิกเงินเดือน ข้อมูลเฉพาะงานและ Output แยกกัน' },
    { title: 'สร้างเอกสาร Word จาก Template', benefit: 'กรอกข้อมูลและเลือกชุดเอกสาร ระบบตรวจข้อมูลที่จำเป็นและแสดงข้อมูลให้ทบทวนก่อนสร้างไฟล์ Word' },
    { title: 'บันทึก โหลด และนำเข้าข้ามชุด', benefit: 'ใช้ข้อมูลเดิมซ้ำได้ เลือกนำเข้าข้อมูลร่วมข้ามงาน พร้อมรายงานช่องว่างและข้อมูลที่นำเข้าไม่ได้' }
  ],
  steps: ['เลือกแท็บงานจัดจ้างหรือเบิกเงินเดือน', 'กรอกข้อมูล หรือกดโหลด Template ที่บันทึกไว้', 'ตรวจข้อมูลและเลือกรายการเอกสารที่ต้องการ', 'ตรวจทานตัวอย่างข้อมูล แล้วยืนยันสร้าง Word', 'เปิดโฟลเดอร์ Output ของงานนั้นและตรวจเอกสาร'],
  screenshot: null, // { src: 'assets/screenshot.png', alt: 'คำอธิบายภาพหน้าจอ' }
  version: '1.0.0',
  supportedSystems: 'Windows · ใช้งานออฟไลน์',
  releaseDate: null,
  installerUrl: 'assets/downloads/sidecontract-Installer.zip',
  sourceUrl: 'assets/downloads/sidecontract-Source.zip',
  guideUrl: 'guide.html',
  readmeUrl: 'README.md',
  guidePdfUrl: null, // เมื่อพร้อม เปลี่ยนเป็น 'guide.pdf'
  changelogUrl: null,
  repositoryUrl: 'https://github.com/RobbyGrean/sidecontract'
};

document.querySelectorAll('[data-name]').forEach(el => el.textContent = SITE_CONFIG.name);
document.title = `${SITE_CONFIG.name} — ดาวน์โหลดโปรแกรม`;
document.querySelector('meta[name="description"]').content = SITE_CONFIG.description;
const headline = document.querySelector('[data-headline]');
headline.replaceChildren();
SITE_CONFIG.headline.split('\n').forEach((line, i) => {
  if (i) headline.append(document.createElement('br'));
  const text = document.createElement(i ? 'span' : 'span');
  text.textContent = line;
  if (!i) text.style.color = 'inherit';
  headline.append(text);
});
document.querySelector('[data-description]').textContent = SITE_CONFIG.description;
document.querySelectorAll('[data-link]').forEach(el => {
  const key = el.dataset.link;
  const url = SITE_CONFIG[key];
  if (url) {
    el.href = url;
    el.removeAttribute('aria-disabled');
    el.removeAttribute('role');
    el.hidden = false;
  } else {
    el.removeAttribute('href');
    el.setAttribute('role', 'link');
    el.setAttribute('aria-disabled', 'true');
    el.setAttribute('tabindex', '0');
    el.addEventListener('click', e => e.preventDefault());
    if (key === 'installerUrl') el.textContent = 'กำลังเตรียมดาวน์โหลด';
    if (key === 'sourceUrl') el.textContent = 'Source code จะเพิ่มภายหลัง';
    if (key.startsWith('guide')) el.setAttribute('aria-describedby', 'guide-status');
    if (key === 'repositoryUrl' || key === 'changelogUrl') el.hidden = true;
  }
});
const metadata = [SITE_CONFIG.version && `รุ่น ${SITE_CONFIG.version}`, SITE_CONFIG.supportedSystems, SITE_CONFIG.releaseDate && `เผยแพร่ ${SITE_CONFIG.releaseDate}`].filter(Boolean);
document.querySelector('[data-metadata]').textContent = metadata.join(' · ');
document.querySelector('[data-metadata]').hidden = !metadata.length;
document.querySelector('[data-installer-note]').textContent = SITE_CONFIG.installerUrl ? `ไฟล์ติดตั้ง .zip${SITE_CONFIG.guideUrl ? ' · มีคู่มือการใช้งานแล้ว' : ' · คู่มือกำลังจัดเตรียม'}` : 'กำลังจัดเตรียมไฟล์โปรแกรม';
SITE_CONFIG.features.forEach(feature => {
  const item = document.createElement('article'); item.className = 'feature-item';
  const title = document.createElement('h3'); title.textContent = feature.title;
  const benefit = document.createElement('p'); benefit.textContent = feature.benefit;
  item.append(title, benefit);
  document.querySelector('#feature-list').append(item);
});
if (SITE_CONFIG.steps.length) {
  const steps = document.querySelector('#steps'); steps.hidden = false;
  SITE_CONFIG.steps.forEach(text => { const li = document.createElement('li'); li.textContent = text; steps.append(li); });
}
const guideReady = Boolean(SITE_CONFIG.guideUrl || SITE_CONFIG.guidePdfUrl);
document.querySelector('#guide-status').textContent = guideReady ? 'คู่มือออนไลน์พร้อมใช้งาน' : 'กำลังจัดเตรียมคู่มือ';
document.querySelector('#guide-note').textContent = SITE_CONFIG.guideUrl && SITE_CONFIG.guidePdfUrl ? 'อ่านออนไลน์หรือเก็บไฟล์ PDF ไว้อ่านภายหลัง' : guideReady ? 'อ่านคู่มือฉบับเต็มได้จากหน้า Guide' : 'คู่มือกำลังจัดเตรียม';


const motionPreference = matchMedia('(prefers-reduced-motion: reduce)');
const finePointer = matchMedia('(hover: hover) and (pointer: fine)');
const downloadObserver = new IntersectionObserver(entries => entries.forEach(entry => entry.target.classList.toggle('motion-paused', !entry.isIntersecting)));
document.querySelectorAll('.primary').forEach(button => downloadObserver.observe(button));
const stage = document.querySelector('.preview-stage');
const frame = stage.querySelector('.preview-frame');
let tiltFrame = 0;
stage.addEventListener('pointermove', event => {
  if (motionPreference.matches || !finePointer.matches) return;
  const rect = stage.getBoundingClientRect();
  const x = (event.clientX - rect.left) / rect.width - 0.5;
  const y = (event.clientY - rect.top) / rect.height - 0.5;
  cancelAnimationFrame(tiltFrame);
  tiltFrame = requestAnimationFrame(() => { frame.style.transform = `perspective(1500px) rotateY(${x * 5}deg) rotateX(${-y * 3}deg)`; });
});
function resetDepth() { cancelAnimationFrame(tiltFrame); frame.style.removeProperty('transform'); }
stage.addEventListener('pointerleave', resetDepth);
motionPreference.addEventListener('change', resetDepth);
finePointer.addEventListener('change', resetDepth);
