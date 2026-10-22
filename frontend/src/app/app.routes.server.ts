import { RenderMode, ServerRoute } from '@angular/ssr'

/**
 * Public pages are prerendered to HTML at build time, so search engines and link previews read
 * them without running scripts. Everything else (panel, personal links, 404) renders in the browser.
 */
export const serverRoutes: ServerRoute[] = [
  ...[
    '',
    'book',
    'services',
    'tariffs',
    'drivers',
    'reviews',
    'partners',
    'privacy',
    'terms',
  ].map(
    path => ({ path, renderMode: RenderMode.Prerender }) satisfies ServerRoute
  ),
  { path: '**', renderMode: RenderMode.Client },
]
