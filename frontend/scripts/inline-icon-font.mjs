// Last step of `npm run generate-fonts`: embeds icons.woff in icons.css. Icons then arrive with
// the styles and are never drawn as empty boxes while a separate font file is still loading.
import { readFileSync, rmSync, writeFileSync } from 'node:fs'

const dir = 'src/assets/fonts'
const woff = readFileSync(`${dir}/icons.woff`).toString('base64')
const css = readFileSync(`${dir}/icons.css`, 'utf8')
const inlined = css.replace(
  /src: url\("\.\/icons\.woff\?[0-9a-f]+"\) format\("woff"\);/,
  `src: url("data:font/woff;base64,${woff}") format("woff");\n    font-display: block;`
)
if (inlined === css) {
  throw new Error('icons.css: @font-face src not found')
}
writeFileSync(`${dir}/icons.css`, inlined)
rmSync(`${dir}/icons.woff`)
console.log(`icons.css: font embedded (${Math.round(woff.length / 1024)} KB base64)`)
