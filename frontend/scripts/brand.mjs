// Before a build: makes the chosen client the active brand (src/brands/active.json) and puts its
// name and icons into index.html, the web app manifest, the app icons and the favicon. BRAND = a folder in src/brands/; without it,
// nothing changes (Magnus Drive). Run by `npm run build:site`, so the Docker build does it.
import { copyFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs'

const id = process.env.BRAND || 'magnus-drive'
const dir = `src/brands/${id}`
if (!existsSync(`${dir}/brand.json`)) {
  console.error(`brand: no ${dir}/brand.json (set BRAND to a folder in src/brands/)`)
  process.exit(1)
}
const brand = JSON.parse(readFileSync(`${dir}/brand.json`, 'utf8'))
// The only brand the app code reads (src/app/core/brand.ts): other clients never reach the bundle.
copyFileSync(`${dir}/brand.json`, 'src/brands/active.json')

for (const size of [180, 192, 512]) {
  copyFileSync(`${dir}/icon-${size}.png`, `src/assets/brand/icon-${size}.png`)
}
copyFileSync(`${dir}/icon.svg`, 'src/assets/brand/icon.svg')
copyFileSync(`${dir}/favicon.ico`, 'src/favicon.ico')

const manifest = JSON.parse(readFileSync('src/manifest.webmanifest', 'utf8'))
manifest.name = brand.name
manifest.short_name = brand.shortName
writeFileSync('src/manifest.webmanifest', JSON.stringify(manifest, null, 2) + '\n')

const escape = text => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;')
let index = readFileSync('src/index.html', 'utf8')
index = index
  .replace(/<title>[^<]*<\/title>/, `<title>${escape(`${brand.name} · ${brand.tagline}`)}</title>`)
  .replace(/(class="car-loader car-loader--lg"\s+role="status"\s+aria-label=")[^"]*"/, `$1${escape(brand.name)}"`)
writeFileSync('src/index.html', index)
console.log(`brand: ${brand.name} (${id})`)
