import { chromium } from 'playwright';
import fs from 'node:fs';
fs.mkdirSync('artifacts/continent-browser', { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true, args: ['--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
page.setDefaultTimeout(120000);
try {
  await page.goto(process.env.WORLDGEN_URL || 'http://127.0.0.1:5080');
  await page.getByText('Advanced import / export',{exact:true}).click();
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  await page.getByLabel('Preset', { exact: true }).selectOption('Preview');
  for (const [label, value] of [['Seed', '42'], ['Continents', '4'], ['Interconnectedness', '0']]) {
    await page.getByLabel(label, { exact: true }).fill(value);
    await page.getByLabel(label, { exact: true }).press('Tab');
  }
  await page.getByRole('button', { name: 'Generate world', exact: false }).click();
  await page.getByRole('alert').waitFor();
  if (!(await page.getByRole('alert').innerText()).includes('Interconnectedness')) throw new Error('Missing candidate-count validation');
  await page.getByLabel('Interconnectedness', { exact: true }).fill('10');
  await page.getByLabel('Interconnectedness', { exact: true }).press('Tab');
  await page.getByRole('button', { name: 'Generate world', exact: false }).click();
  await page.waitForFunction(() => document.querySelector('.top-status')?.textContent === 'World ready', {}, { timeout: 600000 });
  const world = JSON.parse(await page.evaluate(async () => (await import('/globe.js')).snapshot()));
  if (world.Recipe.Seed !== 42 || world.Recipe.Interconnectedness !== 10) throw new Error('Plate-selection settings not applied');
  const expected = JSON.parse(fs.readFileSync(process.env.WORLDGEN_EXPECTED_HASHES || 'tests/golden/v3/interconnection-search.hashes.json', 'utf8'));
  if (world.Stages.length !== expected.length || world.Stages.some((stage, i) => stage.Hash !== expected[i].Hash)) throw new Error('Plate-selection host mismatch');
  const save = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export recipe', exact: true }).click();
  await (await save).saveAs('artifacts/continent-browser/custom.recipe.json');
  await page.locator('.file-button').filter({hasText:'Import recipe'}).locator('input').setInputFiles('artifacts/continent-browser/custom.recipe.json');
  if (await page.getByLabel('Interconnectedness', { exact: true }).inputValue() !== '10') throw new Error('Selection settings not retained on recipe load');
  await page.evaluate(async () => { globalThis.continentView = await import('/globe.js'); });
  async function change(action) {
    const before = await page.evaluate(() => globalThis.continentView.viewInfo().revision);
    await action();
    await page.waitForFunction(before => { const view = globalThis.continentView.viewInfo(); return view.revision > before && !view.loading; }, before);
  }
  await change(() => page.getByLabel('View', { exact: true }).selectOption('Map'));
  for (const layer of ['Continents', 'Terrain']) {
    await change(() => page.getByLabel('Diagnostic layer').selectOption(layer));
    await page.screenshot({ path: `artifacts/continent-browser/${layer}.png`, fullPage: true });
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByLabel('Interconnectedness', { exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: 'artifacts/continent-browser/mobile-selection.png', fullPage: true });
  if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw new Error('Selection controls cause overflow');
  console.log('PASS plate-selection validation, exact save/load, cross-host hashes, diagnostic maps and compact layout');
} finally { await browser.close(); }
