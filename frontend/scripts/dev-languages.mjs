// `npm start`: the site in all four languages at http://localhost:4200, with live reload.
// `ng serve` builds one language at a time, so this runs one dev server per language and sends
// each request to the right one: Italian at /, English, German and French under /en, /de, /fr
// (as Caddy does in production). /api goes through the Italian server's proxy to :5000.
import { spawn } from 'node:child_process'
import http from 'node:http'
import net from 'node:net'

const port = Number(process.env.PORT ?? 4200)
const servers = {
  it: { port: 4201, configuration: 'development' },
  en: { port: 4202, configuration: 'development-en' },
  de: { port: 4203, configuration: 'development-de' },
  fr: { port: 4204, configuration: 'development-fr' },
}

const children = Object.entries(servers).map(([lang, s]) => {
  const child = spawn(
    'npx',
    [
      'ng',
      'serve',
      '--configuration',
      s.configuration,
      '--port',
      String(s.port),
      '--host',
      'localhost',
    ],
    { stdio: ['ignore', 'pipe', 'pipe'] }
  )
  const prefix = `[${lang}] `
  const print = stream => data =>
    stream.write(
      data
        .toString()
        .split('\n')
        .filter(line => line.trim())
        .map(line => prefix + line + '\n')
        .join('')
    )
  child.stdout.on('data', print(process.stdout))
  child.stderr.on('data', print(process.stderr))
  return child
})

const stop = () => {
  for (const child of children) child.kill('SIGTERM')
  process.exit(0)
}
process.on('SIGINT', stop)
process.on('SIGTERM', stop)

/** The dev server for a path: /de/… → German, everything else (and /api) → Italian. */
const target = path => {
  const lang = path.match(/^\/(en|de|fr)(?=\/|\?|$)/)?.[1]
  return servers[lang ?? 'it'].port
}

http
  .createServer((req, res) => {
    const url = req.url ?? '/'
    // As in production: Italian has no prefix; "/de" without the slash gets one.
    if (/^\/it(\/|\?|$)/.test(url)) {
      res.writeHead(301, { location: url.replace(/^\/it/, '') || '/' }).end()
      return
    }
    if (/^\/(en|de|fr)$/.test(url.split('?')[0])) {
      res.writeHead(302, { location: url.replace(/^(\/\w\w)/, '$1/') }).end()
      return
    }
    const upstream = http.request(
      {
        host: 'localhost',
        port: target(url),
        path: url,
        method: req.method,
        headers: req.headers,
      },
      answer => {
        res.writeHead(answer.statusCode ?? 502, answer.headers)
        answer.pipe(res)
      }
    )
    upstream.on('error', () => {
      res
        .writeHead(503, { 'content-type': 'text/plain; charset=utf-8' })
        .end('Still starting… reload in a few seconds.')
    })
    req.pipe(upstream)
  })
  // Live reload uses a WebSocket on the same address: pass it through to the right server.
  .on('upgrade', (req, socket, head) => {
    const upstream = net.connect(target(req.url ?? '/'), 'localhost', () => {
      const headers = Object.entries(req.headers)
        .map(([k, v]) => `${k}: ${v}`)
        .join('\r\n')
      upstream.write(`${req.method} ${req.url} HTTP/1.1\r\n${headers}\r\n\r\n`)
      upstream.write(head)
      socket.pipe(upstream).pipe(socket)
    })
    upstream.on('error', () => socket.destroy())
    socket.on('error', () => upstream.destroy())
  })
  .listen(port, () => {
    console.log(
      `\nAll languages: http://localhost:${port}/ (it) · /en/ · /de/ · /fr/  (Ctrl+C stops all)\n`
    )
  })
