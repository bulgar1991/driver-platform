/**
 * Magnus Drive palette: black + gold.
 * Single source for Tailwind (tailwind.config.ts) and the PrimeNG preset
 * (src/app/theme.ts), so both always use the same colors.
 */
export const colors = {
  ink: {
    DEFAULT: '#0B0B0C',
    950: '#060607',
    900: '#0B0B0C',
    800: '#121214',
    700: '#1A1A1D',
    600: '#232327',
    500: '#2E2E33',
    400: '#4A4A50',
  },
  gold: {
    DEFAULT: '#C9A45C',
    50: '#FBF6EA',
    100: '#F4E8C9',
    200: '#E9D39E',
    300: '#DDBE7A',
    400: '#D3B06A',
    500: '#C9A45C',
    600: '#AD8A47',
    700: '#8C6E37',
    800: '#6B5329',
    900: '#4A391C',
    950: '#2E2311',
  },
  ivory: {
    DEFAULT: '#F5F1E8',
    100: '#E4DFD5',
    200: '#B8B2A7',
    300: '#9A958C',
    400: '#7D786F',
  },
  success: '#4CAF7A',
  warning: '#E0A43A',
  danger: '#D9534F',
  info: '#5B8DEF',
} as const

export type ColorScale = Record<
  50 | 100 | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 950,
  string
>

/** Gold as a full 50–950 scale (PrimeNG primary). */
export const goldScale: ColorScale = {
  50: colors.gold[50],
  100: colors.gold[100],
  200: colors.gold[200],
  300: colors.gold[300],
  400: colors.gold[400],
  500: colors.gold[500],
  600: colors.gold[600],
  700: colors.gold[700],
  800: colors.gold[800],
  900: colors.gold[900],
  950: colors.gold[950],
}

/** Light text (50) down to the page background (950) — PrimeNG dark surface. */
export const surfaceScale: ColorScale & { 0: string } = {
  0: '#FFFFFF',
  50: colors.ivory.DEFAULT,
  100: colors.ivory[100],
  200: colors.ivory[200],
  300: colors.ivory[300],
  400: colors.ivory[400],
  500: colors.ink[400],
  600: colors.ink[500],
  700: colors.ink[600],
  800: colors.ink[700],
  900: colors.ink[800],
  950: colors.ink[900],
}
