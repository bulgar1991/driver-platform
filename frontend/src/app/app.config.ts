import { provideHttpClient, withInterceptors } from '@angular/common/http'
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
import { AuthService } from './core/services/auth.service'
import { MagnusPreset } from './theme'

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      // Route data (title, step, icon) binds straight to component inputs.
      withComponentInputBinding(),
      withInMemoryScrolling({ anchorScrolling: 'enabled' })
    ),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
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
  ],
}
