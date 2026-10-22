// Serves the built site in all four languages, like Caddy in production (deploy/Caddyfile):
// Italian at /, English, German and French under /en, /de, /fr; /api goes to the backend.
// `ng serve` can serve one language only. Used by `npm run start:languages`.
import { createReadStream, existsSync, statSync } from 'node:fs'
import http from 'node:http'
import { extname, join, normalize } from 'node:path'

const root = new URL('../dist/frontend/browser/', import.meta.url).pathname
const port = Number(process.env.PORT ?? 4300)
const api = new URL(process.env.API_URL ?? 'http://localhost:5000')

const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.txt': 'text/plain; charset=utf-8',
  '.xml': 'application/xml',
}

const isFile = path => existsSync(path) && statSync(path).isFile()

/** The file for a path: the file itself, a prerendered page, else the language's app shell. */
function resolve(pathname) {
  const lang = pathname.match(/^\/(en|de|fr)(?=\/|$)/)?.[1]
  const shell = lang ? `${lang}/index.csr.html` : 'index.csr.html'
  const local = normalize(join(root, decodeURIComponent(pathname)))
  if (!local.startsWith(root)) {
    return null
  }
  for (const candidate of [local, join(local, 'index.html')]) {
    if (isFile(candidate)) {
      return candidate
    }
  }
  return join(root, shell)
}

function proxy(req, res) {
  const upstream = http.request(
    {
      hostname: api.hostname,
      port: api.port,
      path: req.url,
      method: req.method,
      headers: { ...req.headers, host: api.host },
    },
    answer => {
      res.writeHead(answer.statusCode ?? 502, answer.headers)
      answer.pipe(res)
    }
  )
  upstream.on('error', () => {
    res.writeHead(502).end(`The API is not running on ${api.origin}.`)
  })
  req.pipe(upstream)
}

http
  .createServer((req, res) => {
    const url = new URL(req.url ?? '/', 'http://localhost')
    if (url.pathname.startsWith('/api/')) {
      return proxy(req, res)
    }
    // As in production: Italian has no prefix.
    if (/^\/it(\/|$)/.test(url.pathname)) {
      const target = url.pathname.replace(/^\/it/, '') || '/'
      res.writeHead(301, { location: target + url.search }).end()
      return
    }
    const file = resolve(url.pathname)
    if (!file || !isFile(file)) {
      res.writeHead(404).end('Not built yet: run npm run start:languages.')
      return
    }
    res.writeHead(200, {
      'content-type': types[extname(file)] ?? 'application/octet-stream',
      'cache-control': 'no-store',
    })
    createReadStream(file).pipe(res)
  })
  .listen(port, () => {
    console.log(
      `All languages: http://localhost:${port}/ (it), /en/, /de/, /fr/`
    )
  })
