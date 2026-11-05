// Renders Jungle Escape's app icons and store graphics from SVG with headless Chrome.
// usage: node make.js <projectDir>
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const project = process.argv[2];
const work = path.join(__dirname, 'html');
const iconDir = path.join(project, 'Assets', 'JungleEscape', 'Icons');
const storeDir = path.join(project, 'StoreAssets');
for (const d of [work, iconDir, storeDir]) fs.mkdirSync(d, { recursive: true });
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';

// ---------------- Art (1024 x 1024 design space) ----------------

const defs = `
<defs>
  <radialGradient id="sky" cx="50%" cy="38%" r="75%">
    <stop offset="0" stop-color="#ffe08a"/>
    <stop offset="0.22" stop-color="#9fd35a"/>
    <stop offset="0.55" stop-color="#2f8a3e"/>
    <stop offset="1" stop-color="#0c3b22"/>
  </radialGradient>
  <radialGradient id="fur" cx="45%" cy="35%" r="70%">
    <stop offset="0" stop-color="#8a5530"/>
    <stop offset="1" stop-color="#4e2c14"/>
  </radialGradient>
  <radialGradient id="skin" cx="50%" cy="35%" r="70%">
    <stop offset="0" stop-color="#ffe0b8"/>
    <stop offset="1" stop-color="#e2aa74"/>
  </radialGradient>
  <linearGradient id="banana" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="#fff27a"/>
    <stop offset="0.6" stop-color="#ffd21f"/>
    <stop offset="1" stop-color="#e59b00"/>
  </linearGradient>
  <linearGradient id="leafA" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="#1f6b30"/>
    <stop offset="1" stop-color="#0a3519"/>
  </linearGradient>
  <linearGradient id="leafB" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="#3a9a45"/>
    <stop offset="1" stop-color="#17552a"/>
  </linearGradient>
  <filter id="soft" x="-20%" y="-20%" width="140%" height="140%">
    <feDropShadow dx="0" dy="14" stdDeviation="16" flood-color="#06210f" flood-opacity="0.55"/>
  </filter>
</defs>`;

// One pointed leaf with a midrib, pointing up from (0,0), length L.
function leaf(x, y, L, W, rot, fill) {
  return `<g transform="translate(${x} ${y}) rotate(${rot})">
    <path d="M0 0 C ${W} ${-L * 0.25}, ${W * 0.8} ${-L * 0.75}, 0 ${-L} C ${-W * 0.8} ${-L * 0.75}, ${-W} ${-L * 0.25}, 0 0 Z" fill="${fill}"/>
    <path d="M0 0 L0 ${-L * 0.96}" stroke="#062a12" stroke-opacity="0.45" stroke-width="${Math.max(3, W * 0.06)}" fill="none"/>
  </g>`;
}

function rays(cx, cy, n, r) {
  let s = '';
  for (let i = 0; i < n; i++) {
    const a0 = (i / n) * Math.PI * 2, a1 = a0 + Math.PI / n;
    s += `<path d="M${cx} ${cy} L${cx + Math.cos(a0) * r} ${cy + Math.sin(a0) * r} L${cx + Math.cos(a1) * r} ${cy + Math.sin(a1) * r} Z"/>`;
  }
  return `<g fill="#fff6c8" opacity="0.13">${s}</g>`;
}

// Full-bleed jungle backdrop.
function background(w = 1024, h = 1024) {
  const cx = w / 2, cy = h * 0.38;
  return `
  <rect width="${w}" height="${h}" fill="url(#sky)"/>
  ${rays(cx, cy, 18, Math.max(w, h))}
  ${leaf(-30, h + 40, 520, 150, 38, 'url(#leafA)')}
  ${leaf(60, h + 60, 420, 120, 12, 'url(#leafB)')}
  ${leaf(w + 30, h + 40, 520, 150, -38, 'url(#leafA)')}
  ${leaf(w - 60, h + 60, 420, 120, -12, 'url(#leafB)')}
  ${leaf(-40, -20, 420, 120, 140, 'url(#leafA)')}
  ${leaf(w + 40, -20, 420, 120, -140, 'url(#leafA)')}`;
}

// The cheeky monkey with a banana, centred on (512, 512), about 760 px wide.
const monkey = `
<g filter="url(#soft)">
  <!-- ears -->
  <circle cx="230" cy="500" r="112" fill="url(#fur)"/>
  <circle cx="794" cy="500" r="112" fill="url(#fur)"/>
  <circle cx="236" cy="504" r="66" fill="#e7a978"/>
  <circle cx="788" cy="504" r="66" fill="#e7a978"/>
  <!-- head + tuft -->
  <path d="M470 228 C 480 170, 520 150, 540 120 C 545 165, 580 180, 600 150 C 600 200, 575 225, 560 236 Z" fill="#4e2c14"/>
  <circle cx="512" cy="520" r="300" fill="url(#fur)"/>
  <!-- face mask -->
  <g fill="url(#skin)">
    <circle cx="430" cy="480" r="132"/>
    <circle cx="594" cy="480" r="132"/>
    <ellipse cx="512" cy="640" rx="222" ry="160"/>
  </g>
  <!-- brows (mischievous) -->
  <path d="M352 380 Q 420 340 486 382" stroke="#3a1f0c" stroke-width="22" stroke-linecap="round" fill="none"/>
  <path d="M538 370 Q 610 326 676 360" stroke="#3a1f0c" stroke-width="22" stroke-linecap="round" fill="none"/>
  <!-- eyes looking right -->
  <ellipse cx="438" cy="478" rx="62" ry="74" fill="#fff"/>
  <ellipse cx="588" cy="478" rx="62" ry="74" fill="#fff"/>
  <circle cx="462" cy="486" r="36" fill="#2a1608"/>
  <circle cx="612" cy="486" r="36" fill="#2a1608"/>
  <circle cx="474" cy="472" r="12" fill="#fff"/>
  <circle cx="624" cy="472" r="12" fill="#fff"/>
  <!-- nose -->
  <ellipse cx="490" cy="598" rx="14" ry="10" fill="#6b3a1a"/>
  <ellipse cx="536" cy="598" rx="14" ry="10" fill="#6b3a1a"/>
  <!-- grin -->
  <path d="M388 652 Q 512 790 650 640 Q 520 700 388 652 Z" fill="#7a1f1a"/>
  <path d="M410 660 Q 520 700 630 650 L 622 672 Q 520 712 418 680 Z" fill="#fff"/>
  <path d="M388 652 Q 512 790 650 640" stroke="#3a1f0c" stroke-width="12" stroke-linecap="round" fill="none"/>
  <!-- banana, held up in a hand -->
  <g transform="translate(690 700) rotate(-28)">
    <path d="M-150 40 C -120 160, 120 170, 190 20 C 150 90, -60 110, -150 40 Z" fill="url(#banana)" stroke="#a86a00" stroke-width="8" stroke-linejoin="round"/>
    <path d="M-120 60 C -60 120, 90 120, 160 50" stroke="#fff8b0" stroke-width="10" stroke-linecap="round" fill="none" opacity="0.7"/>
    <path d="M-150 40 l-26 -14 l8 -16 l26 12 Z" fill="#5a3a12"/>
    <circle cx="190" cy="20" r="9" fill="#5a3a12"/>
  </g>
  <g fill="url(#fur)" stroke="#3a1f0c" stroke-width="6">
    <ellipse cx="640" cy="770" rx="70" ry="56"/>
    <ellipse cx="596" cy="742" rx="26" ry="34"/>
  </g>
</g>`;

const svg = (w, h, body, extra = '') =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" ${extra}>${defs}${body}</svg>`;

// ---------------- Compositions ----------------

const full = (s) => svg(s, s, `<g transform="scale(${s / 1024})">${background()}${monkey}</g>`);

const clipped = (s, shape) => svg(s, s, `
  <clipPath id="c">${shape}</clipPath>
  <g clip-path="url(#c)"><g transform="scale(${s / 1024})">${background()}${monkey}</g></g>`);

// Adaptive layers on a 108dp grid: the foreground must sit inside the 66dp safe circle (61% of the side).
const adaptiveBg = (s) => svg(s, s, `<g transform="scale(${s / 1024})">${background()}</g>`);
const adaptiveFg = (s) => {
  const k = (s / 1024) * 0.66;               // monkey is ~800 px of 1024 -> fits the safe circle
  const off = (s - 1024 * k) / 2;
  return svg(s, s, `<g transform="translate(${off} ${off}) scale(${k})">${monkey}</g>`);
};

const feature = svg(1024, 500, `
  ${background(1024, 500)}
  <g transform="translate(600 28) scale(0.44)">${monkey}</g>
  <g font-family="Georgia, 'Times New Roman', serif" font-weight="bold" text-anchor="middle">
    <text x="330" y="205" font-size="104" fill="#3a1f0c" opacity="0.55" transform="translate(5 7)">JUNGLE</text>
    <text x="330" y="205" font-size="104" fill="#ffd34d" stroke="#5a2e0c" stroke-width="5" paint-order="stroke">JUNGLE</text>
    <text x="330" y="318" font-size="104" fill="#3a1f0c" opacity="0.55" transform="translate(5 7)">ESCAPE</text>
    <text x="330" y="318" font-size="104" fill="#ffd34d" stroke="#5a2e0c" stroke-width="5" paint-order="stroke">ESCAPE</text>
    <text x="330" y="380" font-size="34" font-style="italic" font-weight="normal" fill="#fff8e6">Outrun the monkey troop!</text>
  </g>`);

// ---------------- Render ----------------

function render(name, markup, w, h, outDir) {
  const html = path.join(work, name + '.html');
  fs.writeFileSync(html, `<!doctype html><html><head><style>html,body{margin:0;background:transparent;overflow:hidden}svg{display:block}</style></head><body>${markup}</body></html>`);
  const out = path.join(outDir, name + '.png');
  execFileSync(CHROME, ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1',
    '--default-background-color=00000000', `--window-size=${w},${h}`, `--screenshot=${out}`,
    'file:///' + html.replace(/\\/g, '/')], { stdio: 'ignore' });
  console.log(out);
}

// Unity (it scales these down to every density itself)
render('icon_adaptive_background', adaptiveBg(432), 432, 432, iconDir);
render('icon_adaptive_foreground', adaptiveFg(432), 432, 432, iconDir);
render('icon_default', full(1024), 1024, 1024, iconDir);

// Play Console
render('play_icon_512', full(512), 512, 512, storeDir);
render('feature_graphic_1024x500', feature, 1024, 500, storeDir);
