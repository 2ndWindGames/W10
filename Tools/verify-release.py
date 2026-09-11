"""Validate local Android/store deliverables; exits nonzero on a real failure."""
from pathlib import Path
import json
import struct
import sys
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
results = []

def require(ok, message):
    if not ok:
        raise AssertionError(message)
    results.append('PASS ' + message)

for filename, limit in [('title.txt', 30), ('short-description.txt', 80), ('full-description.txt', 4000)]:
    value = (ROOT / 'Store/ko-KR' / filename).read_text(encoding='utf-8').strip()
    require(0 < len(value) <= limit, f'{filename}: {len(value)}/{limit} characters')

listing = json.loads((ROOT / 'Store/listing.json').read_text(encoding='utf-8'))
require(listing['packageName'] == 'com.secondwindgames.openednote', 'Package identifier matches listing')
require(not listing['containsAds'] and not listing['inAppPurchases'], 'Listing matches no ads / no purchases')
for locale in ['ko-KR', 'en-US']:
    for filename, limit in [('title.txt',30),('short-description.txt',80),('full-description.txt',4000)]:
        value=(ROOT/'Store'/locale/filename).read_text(encoding='utf-8').strip()
        require(0<len(value)<=limit,f'{locale}/{filename} within Play character limit')

def png_size(path):
    content = path.read_bytes()
    require(content[:8] == b'\x89PNG\r\n\x1a\n', f'{path.name} is PNG')
    return struct.unpack('>II', content[16:24]), content[25]

for name, size in [('icon-512.png', (512, 512)), ('feature-1024x500.png', (1024, 500))]:
    dimensions, color_type = png_size(ROOT / 'Store/Graphics' / name)
    require(dimensions == size, f'{name} correct dimensions')
    if name.startswith('feature'):
        require(color_type == 2, 'Feature graphic is RGB without alpha')
for index, name in enumerate(['home', 'detail', 'editor', 'archive'], 1):
    file = ROOT / 'Store/Graphics' / f'phone-0{index}-{name}.png'
    dimensions, color_type = png_size(file)
    require(dimensions == (1080, 1920) and color_type == 2, f'{file.name}: 1080x1920 RGB')

aab = ROOT / 'Builds/Android/OpenedNote-1.0.0-unsigned.aab'
with zipfile.ZipFile(aab) as bundle:
    names = bundle.namelist()
    require('BundleConfig.pb' in names and 'base/manifest/AndroidManifest.xml' in names, 'Valid bundle structure')
    require(not any(n.upper().endswith(('.RSA', '.DSA', '.EC', '.SF')) and n.startswith('META-INF/') for n in names), 'No upload/test signature in AAB')
    libs = [n for n in names if n.endswith('.so')]
    require(libs and all('/arm64-v8a/' in n for n in libs), 'Only ARM64 native libraries')
    for name in libs:
        data = bundle.read(name)
        require(data[:6] == b'\x7fELF\x02\x01', f'{Path(name).name}: ELF64 little endian')
        phoff = struct.unpack_from('<Q', data, 32)[0]
        phentsize, phnum = struct.unpack_from('<HH', data, 54)
        alignments = [struct.unpack_from('<Q', data, phoff + i * phentsize + 48)[0]
                      for i in range(phnum)
                      if struct.unpack_from('<I', data, phoff + i * phentsize)[0] == 1]
        require(alignments and all(a >= 16384 for a in alignments), f'{Path(name).name}: LOAD segment alignment >=16KB')

ns='{http://schemas.android.com/apk/res/android}'
manifest=ET.parse(ROOT/'BuildArtifacts/QA/bundle-manifest.xml').getroot()
require(manifest.attrib['package']==listing['packageName'],'Final bundle package matches store metadata')
require(manifest.attrib[ns+'versionName']=='1.0.0' and manifest.attrib[ns+'versionCode']=='1','Final version is 1.0.0 (1)')
sdk=manifest.find('uses-sdk')
require(sdk.attrib[ns+'minSdkVersion']=='26' and sdk.attrib[ns+'targetSdkVersion']=='36','Final min API 26 and target API 36')
permissions={x.attrib[ns+'name'] for x in manifest.findall('uses-permission')}
require(permissions=={'android.permission.POST_NOTIFICATIONS','android.permission.RECEIVE_BOOT_COMPLETED'},'Only notifications and boot permissions; no network/media/camera/exact alarm permission')
app=manifest.find('application')
require(app.attrib[ns+'allowBackup']=='false' and app.attrib[ns+'fullBackupContent']=='false','Cloud backup disabled')
require(ns+'dataExtractionRules' in app.attrib,'Explicit device-transfer exclusion rules present')
require(app.attrib.get(ns+'debuggable','false')=='false','Release is not debuggable')
require('PAGE_ALIGNMENT_16K' in (ROOT/'BuildArtifacts/QA/bundle-config.json').read_text(encoding='utf-8-sig'),'Bundle requests 16KB page alignment')
apk=ROOT/'Builds/Android/OpenedNote-1.0.0-unsigned.apk'
require(b'APK Sig Block 42' not in apk.read_bytes(),'APK has no v2/v3 signing block')
with zipfile.ZipFile(apk) as package:
    require(not any(n.startswith('META-INF/') and n.upper().endswith(('.RSA','.DSA','.EC','.SF')) for n in package.namelist()),'APK has no v1 signature')
require((ROOT/'Builds/Android/OpenedNote-1.0.0-native-symbols.zip').is_file(),'Native debug symbols prepared')

report = '\n'.join(results) + f'\nTOTAL {len(results)} PASSED\n'
print(report)
(ROOT / 'BuildArtifacts/QA').mkdir(parents=True, exist_ok=True)
(ROOT / 'BuildArtifacts/QA/release-checks.txt').write_text(report, encoding='utf-8')
