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
import { providePrimeNG } from 'primeng/config'
import { firstValueFrom } from 'rxjs'
import { routes } from './app.routes'
import { authInterceptor } from './core/interceptors/auth.interceptor'
import { errorInterceptor } from './core/interceptors/error.interceptor'
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
    // The browser picks up the prerendered page instead of drawing it again. No event replay:
    // its inline scripts would be blocked by the Content-Security-Policy (deploy/Caddyfile).
    provideClientHydration(),
  ],
}
