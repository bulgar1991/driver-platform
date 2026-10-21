import { catchError, EMPTY, Observable, switchMap, timer } from 'rxjs'

/** How often the dispatcher pages refresh, so statuses stay current. */
export const REFRESH_MS = 30_000

/** Runs `load` now and then every `everyMs`; a failed refresh is skipped, not fatal. */
export function poll<T>(
  load: () => Observable<T>,
  everyMs = REFRESH_MS,
  onError?: (err: unknown) => void
): Observable<T> {
  return timer(0, everyMs).pipe(
    switchMap(() =>
      load().pipe(
        catchError(err => {
          onError?.(err)
          return EMPTY
        })
      )
    )
  )
}
