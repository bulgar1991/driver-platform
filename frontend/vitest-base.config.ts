import { resolve } from 'node:path'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  resolve: {
    alias: [
      {
        find: /^moment\/moment$/,
        replacement: resolve(__dirname, 'node_modules/moment/moment.js'),
      },
    ],
  },
})
