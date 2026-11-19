import {
  provideHttpClient,
  withFetch,
  withInterceptors,
} from '@angular/common/http'
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core'
import {
  provideRouter,
  withComponentInputBinding,
  withInMemoryScrolling,
} from '@angular/router'
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field'
import { MAT_SELECT_CONFIG } from '@angular/material/select'
import { providePrimeNG } from 'primeng/config'
import { firstValueFrom } from 'rxjs'
import { routes } from './app.routes'
import { authInterceptor } from './core/interceptors/auth.interceptor'
import { errorInterceptor } from './core/interceptors/error.interceptor'
import { languageInterceptor } from './core/interceptors/language.interceptor'
import { serverApiInterceptor } from './core/interceptors/server-api.interceptor'
import { AuthService } from './core/services/auth.service'
import { MagnusPreset } from './theme'
import { provideClientHydration } from '@angular/platform-browser'

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      // Route data (title, step, icon) binds straight to component inputs.
      withComponentInputBinding(),
      // A new page opens at the top; back/forward restore the position.
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled' })
    ),
    provideHttpClient(
      withFetch(),
      withInterceptors([
        serverApiInterceptor,
        authInterceptor,
        languageInterceptor,
        errorInterceptor,
      ])
    ),
    provideAppInitializer(() =>
      firstValueFrom(inject(AuthService).restoreSession())
    ),
    providePrimeNG({
      theme: {
        preset: MagnusPreset,
        // The site is always dark; `app-dark` is set on <html> in index.html.
        options: { darkModeSelector: '.app-dark' },
      },
    }),
    // Angular Material: outlined fields everywhere, room for the error under each one (so
    // showing it never moves the fields below).
    {
      provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
      useValue: {
        appearance: 'outline',
        floatLabel: 'always',
        subscriptSizing: 'fixed',
        hideRequiredMarker: true,
      },
    },
    {
      provide: MAT_SELECT_CONFIG,
      useValue: {
        hideSingleSelectionIndicator: true,
        // The list opens over the page (above the header, the map), and upwards when there is
        // no room below.
        overlayPanelClass: 'app-select-panel',
      },
    },
    // The browser picks up the prerendered page instead of drawing it again. No event replay:
    // its inline scripts would be blocked by the Content-Security-Policy (deploy/Caddyfile).
    provideClientHydration(),
  ],
}
