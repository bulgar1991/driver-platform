// After `ng build`: writes sitemap.xml (every prerendered page in every language, with its
// alternates) and points robots.txt at it. The site address comes from SITE_URL (https://…);
// without it nothing is written.
import { readFileSync, writeFileSync } from 'node:fs'

const OUT = 'dist/frontend/browser'
const LANGUAGES = [
  { code: 'it', prefix: '' },
  { code: 'en', prefix: '/en' },
  { code: 'de', prefix: '/de' },
  { code: 'fr', prefix: '/fr' },
]
const site = (process.env.SITE_URL ?? '').replace(/\/+$/, '')
if (!/^https?:\/\/[^/]+$/.test(site)) {
  console.warn(
    'seo-files: SITE_URL not set (e.g. https://magnusdrive.it); no sitemap written.'
  )
  process.exit(0)
}

// The prerendered routes include every language; group them by page ("/de/tariffs" → "/tariffs").
const { routes } = JSON.parse(
  readFileSync('dist/frontend/prerendered-routes.json', 'utf8')
)
const pages = new Set(
  Object.keys(routes).map(route => {
    const language = LANGUAGES.find(
      l => l.prefix && (route === l.prefix || route.startsWith(`${l.prefix}/`))
    )
    return (language ? route.slice(language.prefix.length) : route) || '/'
  })
)
const url = (prefix, page) => `${site}${prefix}${page === '/' ? '/' : page}`
const today = new Date().toISOString().slice(0, 10)
const entries = [...pages]
  .sort((a, b) => a.length - b.length || a.localeCompare(b))
  .flatMap(page =>
    LANGUAGES.map(language => {
      const alternates = [
        ...LANGUAGES.map(l => [l.code, l.prefix]),
        ['x-default', ''],
      ]
        .map(
          ([code, prefix]) =>
            `    <xhtml:link rel="alternate" hreflang="${code}" href="${url(prefix, page)}"/>`
        )
        .join('\n')
      return `  <url>
    <loc>${url(language.prefix, page)}</loc>
${alternates}
    <lastmod>${today}</lastmod>
  </url>`
    })
  )
writeFileSync(
  `${OUT}/sitemap.xml`,
  `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">
${entries.join('\n')}
</urlset>
`
)
const robots = readFileSync(`${OUT}/robots.txt`, 'utf8').trimEnd()
writeFileSync(
  `${OUT}/robots.txt`,
  `${robots}\n\nSitemap: ${site}/sitemap.xml\n`
)
console.log(
  `seo-files: sitemap.xml with ${entries.length} pages (${pages.size} × ${LANGUAGES.length} languages) for ${site}`
)
