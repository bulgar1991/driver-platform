// Part of `npm run i18n`: after extraction, lists what each translation file lacks.
// src/locale/messages.json is the English source; messages.<lang>.json are the translations.
import { readFileSync } from 'node:fs'

const read = file => JSON.parse(readFileSync(`src/locale/${file}`, 'utf8')).translations
const placeholders = text => (text.match(/\{\$[\w]+\}/g) ?? []).sort().join(' ')
const source = read('messages.json')
let problems = 0

for (const lang of ['it', 'de', 'fr']) {
  const translations = read(`messages.${lang}.json`)
  const missing = Object.keys(source).filter(id => !(id in translations))
  const obsolete = Object.keys(translations).filter(id => !(id in source))
  const broken = Object.keys(source).filter(
    id => id in translations && placeholders(source[id]) !== placeholders(translations[id])
  )
  for (const id of missing) console.log(`${lang}  missing   ${id}: ${source[id]}`)
  for (const id of broken) console.log(`${lang}  placeholders differ  ${id}: ${source[id]}`)
  for (const id of obsolete) console.log(`${lang}  no longer used  ${id}`)
  problems += missing.length + broken.length
}
console.log(problems ? `\n${problems} translation(s) to fix.` : 'All translations complete.')
process.exit(problems ? 1 : 0)
