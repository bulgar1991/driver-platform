import { DatePipe } from '@angular/common'
import { HttpClient } from '@angular/common/http'
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  LOCALE_ID,
  OnInit,
  signal,
} from '@angular/core'
import { finalize } from 'rxjs'
import { siteLocale } from '@/core/i18n/locales'
import { VisitorSummary } from '@/core/models'
import { API_BASE_URL } from '@/core/tokens/api-base-url.token'
import { problemMessage } from '@/core/utils/problem'

/**
 * Admin → Visitors: how many people visit the public site, from which countries and cities, on
 * which pages and devices, and the latest visits with their IP (kept 30 days).
 */
@Component({
  selector: 'app-visitors',
  imports: [DatePipe],
  templateUrl: './visitors.component.html',
  styleUrl: './visitors.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VisitorsComponent implements OnInit {
  private readonly http = inject(HttpClient)
  private readonly url = `${inject(API_BASE_URL)}/admin/visitors`
  private readonly regionNames = new Intl.DisplayNames(
    [siteLocale(inject(LOCALE_ID)).code],
    { type: 'region' }
  )

  protected readonly periods = [
    { days: 1, label: $localize`Today` },
    { days: 7, label: $localize`7 days` },
    { days: 30, label: $localize`30 days` },
    { days: 90, label: $localize`90 days` },
  ]
  protected readonly devices: Record<string, string> = {
    desktop: $localize`Computer`,
    mobile: $localize`Phone`,
    tablet: $localize`Tablet`,
  }
  protected readonly days = signal(30)
  protected readonly summary = signal<VisitorSummary | null>(null)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)

  /** Bar heights against the busiest day. */
  protected readonly peak = computed(() =>
    Math.max(1, ...(this.summary()?.byDay ?? []).map(d => d.visits))
  )

  ngOnInit(): void {
    this.load(this.days())
  }

  protected load(days: number): void {
    this.days.set(days)
    this.loading.set(true)
    this.error.set(null)
    this.http
      .get<VisitorSummary>(this.url, { params: { days } })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: s => this.summary.set(s),
        error: err => this.error.set(problemMessage(err)),
      })
  }

  /** "CH" → "🇨🇭 Switzerland". */
  protected country(code: string | null): string {
    if (!code) {
      return '—'
    }
    const flag = String.fromCodePoint(
      ...[...code.toUpperCase()].map(c => 0x1f1a5 + c.charCodeAt(0))
    )
    let name = code
    try {
      name = this.regionNames.of(code.toUpperCase()) ?? code
    } catch {
      // Unknown code: shown as it is.
    }
    return `${flag} ${name}`
  }

  protected share(count: number, total: number): number {
    return total ? Math.round((count / total) * 100) : 0
  }
}
