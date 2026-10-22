import { HttpErrorResponse } from '@angular/common/http'
import { ProblemDetails } from '../models'

/** A readable message from an API error: the first field error, else the title. */
export function problemMessage(
  err: unknown,
  fallback = $localize`Something went wrong. Please try again.`
): string {
  if (!(err instanceof HttpErrorResponse)) {
    return fallback
  }
  if (err.status === 0) {
    return $localize`Could not reach the server. Check your connection and try again.`
  }
  if (err.status === 429) {
    return $localize`Too many requests. Wait a minute and try again.`
  }
  const problem = err.error as ProblemDetails | null
  const firstError = problem?.errors
    ? Object.values(problem.errors).flat()[0]
    : undefined
  return firstError ?? problem?.title ?? fallback
}
