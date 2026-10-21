import { definePreset } from '@primeuix/themes'
import Aura from '@primeuix/themes/aura'
import { colors, goldScale, surfaceScale } from '../theme/colors'

/** PrimeNG preset built from the same palette as tailwind.config.ts. */
export const MagnusPreset = definePreset(Aura, {
  semantic: {
    primary: goldScale,
    colorScheme: {
      dark: {
        surface: surfaceScale,
        primary: {
          color: '{primary.500}',
          contrastColor: colors.ink.DEFAULT,
          hoverColor: '{primary.400}',
          activeColor: '{primary.300}',
        },
      },
    },
  },
})
