// Icon font: `npm run generate-fonts` (outlines the stroked sources first, then builds the font).
// Sources: src/assets/icons/src/*.svg (24×24, stroke 1.5). Do not edit font-icons/ or the outputs by hand.
module.exports = {
  inputDir: './src/assets/icons/font-icons',
  outputDir: './src/assets/fonts',
  name: 'icons',
  prefix: 'icon',
  // woff works in every current browser (this ttf2woff2 build cannot produce woff2). It is then
  // embedded in icons.css by scripts/inline-icon-font.mjs.
  fontTypes: ['woff'],
  assetTypes: ['css', 'ts'],
  normalize: true,
  fontHeight: 1000,
  pathOptions: {
    ts: './src/app/core/models/icon-names.generated.ts',
  },
  formatOptions: {
    ts: { types: ['literalId'], literalIdName: 'IconName' },
  },
}
