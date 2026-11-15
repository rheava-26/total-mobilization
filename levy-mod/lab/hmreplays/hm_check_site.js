// Opens fights of a built replay site in headless Chromium and reports pageerrors / "Could not load".
//   node hm_check_site.js SITE_DIR PORT [PER_SET=3]
// Starts its own `python3 -m http.server PORT` on SITE_DIR and stops it at the end. Picks, per set, the first Master+Death fight, the first Expert+Rev fight and the largest fight.
const { chromium } = require('playwright');
const { spawn } = require('child_process');
const fs = require('fs'), path = require('path');
(async () => {
  const [site, port, per] = [process.argv[2], process.argv[3] || '8933', +(process.argv[4] || 3)];
  const srv = spawn('python3', ['-m', 'http.server', port, '--bind', '127.0.0.1', '--directory', site], { stdio: 'ignore' });
  await new Promise(r => setTimeout(r, 1500));
  const man = JSON.parse(fs.readFileSync(path.join(site, 'data', 'manifest.json')));
  const browser = await chromium.launch();
  let bad = 0, n = 0;
  for (const set of man.sets) {
    const fs_ = set.fights.filter(f => fs.existsSync(path.join(site, f.file))), picks = [];
    const add = f => { if (f && !picks.includes(f)) picks.push(f); };
    add(fs_.find(f => f.mode.startsWith('Master'))); add(fs_.find(f => !f.mode.startsWith('Master'))); add([...fs_].sort((a, b) => b.bytes - a.bytes)[0]);
    for (const f of picks.slice(0, per)) {
      const page = await browser.newPage(); const errs = [];
      page.on('pageerror', e => errs.push(String(e).slice(0, 200)));
      await page.goto(`http://127.0.0.1:${port}/index.html#f=${encodeURIComponent(f.id)}`);
      let msg = '', loaded = false;
      for (let i = 0; i < 60; i++) {
        await page.waitForTimeout(500);
        msg = await page.evaluate(() => { const m = document.getElementById('msg'); return m ? (m.classList.contains('show') ? (document.getElementById('msgText') || m).textContent : '') : 'nomsg'; });
        if (msg === '') { loaded = true; break; }
        if (/Could not load/.test(msg)) break;
      }
      await page.waitForTimeout(800);
      const ok = loaded && errs.length === 0 && !/Could not load/.test(msg);
      n++; if (!ok) bad++;
      console.log((ok ? 'OK   ' : 'FAIL ') + set.title.slice(0, 48).padEnd(48) + ' ' + f.id.slice(-40).padEnd(40) + (errs.length ? ' pageerror: ' + errs[0] : '') + (msg ? ' msg: ' + msg.slice(0, 80) : ''));
      await page.close();
    }
  }
  await browser.close(); srv.kill();
  console.log(`${n - bad}/${n} fights loaded clean`);
  process.exit(bad ? 1 : 0);
})();
