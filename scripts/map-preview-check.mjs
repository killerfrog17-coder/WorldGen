// Rasterize the CLI's shared-presentation SVG previews; no generation runs here.
import { chromium } from 'playwright';
import { pathToFileURL } from 'node:url';
import path from 'node:path';
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 720 }, deviceScaleFactor: 1 });
  for (const file of process.argv.slice(2)) {
    await page.goto(pathToFileURL(path.resolve(file)).href);
    await page.screenshot({ path: file.replace(/\.svg$/, '.png') });
  }
} finally { await browser.close(); }
