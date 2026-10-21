import type { Config } from 'tailwindcss'
import flowbite from 'flowbite/plugin'
import { colors } from './src/theme/colors'

export default {
  content: ['./src/**/*.{html,ts,scss}', './node_modules/flowbite/**/*.js'],
  theme: {
    extend: {
      colors,
      fontFamily: {
        display: ['Cinzel', 'Georgia', 'serif'],
        sans: ['Montserrat', 'system-ui', 'sans-serif'],
      },
      screens: {
        xs: '360px',
        xm: '520px',
      },
      backgroundImage: {
        'gold-gradient': `linear-gradient(135deg, ${colors.gold[100]} 0%, ${colors.gold[500]} 45%, ${colors.gold[700]} 100%)`,
        'gold-line': `linear-gradient(90deg, transparent, ${colors.gold[500]} 50%, transparent)`,
      },
      boxShadow: {
        gold: '0 0 0 1px rgb(201 164 92 / 0.35), 0 8px 30px rgb(201 164 92 / 0.12)',
      },
      letterSpacing: {
        luxe: '0.3em',
      },
    },
  },
  plugins: [flowbite],
} satisfies Config
