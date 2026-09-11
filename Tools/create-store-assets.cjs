const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'Store/Graphics');
const brand = path.join(root, 'Assets/OpenedNote/Branding');
fs.mkdirSync(out, {recursive:true});
const font = 'Malgun Gothic, Noto Sans KR, sans-serif';
// Editable vector source shared by the launcher and store art.
const mark = `<g fill="none" stroke="#fffdf7" stroke-width="19" stroke-linecap="round" stroke-linejoin="round"><path d="M172 131h168v46H172z"/><path d="M182 179l-24 36v148a25 25 0 0 0 25 25h146a25 25 0 0 0 25-25V215l-24-36"/><path d="m205 278 37 36 68-72" stroke-width="23"/><path d="m373 98 12-26m10 56 28-4" stroke="#ffe0b7" stroke-width="15"/></g>`;
const icon = `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512"><rect width="512" height="512" fill="#ed672e"/>${mark}</svg>`;
const feature = `<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="500"><rect width="1024" height="500" fill="#fcf7ee"/><circle cx="900" cy="210" r="278" fill="#f6ead5"/><circle cx="876" cy="235" r="182" fill="#f9eedf"/>
<text x="72" y="94" fill="#b27b55" font-family="${font}" font-size="16" font-weight="700" letter-spacing="4">OPENED NOTE</text>
<text x="66" y="215" fill="#292b29" font-family="${font}" font-size="80" font-weight="700">개봉노트</text>
<text x="72" y="290" fill="#6f6a61" font-family="${font}" font-size="29">처음 연 날부터, 다 쓴 날까지.</text>
<text x="72" y="398" fill="#b5562f" font-family="${font}" font-size="21">개봉일 기록 · 내가 정한 알림 · 보관함</text>
<rect x="678" y="114" width="267" height="267" rx="63" fill="#ed672e"/><g transform="translate(678 114) scale(.5215)">${mark}</g></svg>`;
(async () => {
  fs.writeFileSync(path.join(out,'icon-source.svg'), icon);
  fs.writeFileSync(path.join(out,'feature-source.svg'), feature);
  await sharp(Buffer.from(icon)).png().toFile(path.join(out,'icon-512.png'));
  await sharp(Buffer.from(feature)).flatten({background:'#fcf7ee'}).png().toFile(path.join(out,'feature-1024x500.png'));
  fs.copyFileSync(path.join(out,'icon-512.png'),path.join(brand,'OpenedNoteIcon.png'));
  await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="432" height="432"><g transform="translate(42 42) scale(.68)">${mark}</g></svg>`)).png().toFile(path.join(brand,'AdaptiveForeground.png'));
  await sharp({create:{width:432,height:432,channels:3,background:'#ed672e'}}).png().toFile(path.join(brand,'AdaptiveBackground.png'));
  for (const [i,name] of ['home','detail','editor','archive'].entries()) {
    const src=path.join(root,`BuildArtifacts/QA/${name}-1080x1920.png`);
    if(fs.existsSync(src)) await sharp(src).flatten({background:'#fcfaf6'}).png().toFile(path.join(out,`phone-0${i+1}-${name}.png`));
  }
  console.log('Generated store icon, feature image, launcher assets and available runtime captures.');
})();
