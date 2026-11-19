// Makes a brand's app icons and favicon from its icon.svg (run after changing the svg, then commit):
//   node scripts/brand-icons.mjs helvetic-vip-transfer
import { writeFileSync } from 'node:fs'
import sharp from 'sharp'

const brand = process.argv[2]
if (!brand) {
  console.error('usage: node scripts/brand-icons.mjs <brand folder in src/brands>')
  process.exit(1)
}
const dir = `src/brands/${brand}`
const icon = `${dir}/icon.svg`
for (const size of [180, 192, 512]) {
  await sharp(icon).resize(size, size).png({ compressionLevel: 9, palette: true }).toFile(`${dir}/icon-${size}.png`)
}
// favicon.ico holding one 32×32 PNG (an ICO header and a single directory entry, then the PNG).
const png32 = await sharp(icon).resize(32, 32).png().toBuffer()
const header = Buffer.alloc(22)
header.writeUInt16LE(0, 0)
header.writeUInt16LE(1, 2)
header.writeUInt16LE(1, 4)
header.writeUInt8(32, 6)
header.writeUInt8(32, 7)
header.writeUInt16LE(1, 10)
header.writeUInt16LE(32, 12)
header.writeUInt32LE(png32.length, 14)
header.writeUInt32LE(22, 18)
writeFileSync(`${dir}/favicon.ico`, Buffer.concat([header, png32]))
console.log(`icons written to ${dir}`)
