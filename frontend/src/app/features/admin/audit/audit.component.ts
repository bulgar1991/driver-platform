import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { takeUntilDestroyed } from '@angular/core/rxjs-interop'
import { FormControl, ReactiveFormsModule } from '@angular/forms'
import { debounceTime } from 'rxjs'
import { AuditEntry } from '@/core/models'
import { AdminStaffService } from '@/core/services/admin-staff.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'

/** Business plan §6: who changed what, when, from which IP. Read-only. */
@Component({
  selector: 'app-audit',
  imports: [DatePipe, ReactiveFormsModule, ButtonComponent],
  templateUrl: './audit.component.html',
  styleUrl: './audit.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditComponent implements OnInit {
  private readonly api = inject(AdminStaffService)
  protected readonly search = new FormControl('', { nonNullable: true })
  protected readonly entries = signal<AuditEntry[]>([])
  protected readonly next = signal<number | null>(null)
  protected readonly loading = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly expanded = signal<number | null>(null)

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.load(false))
  }

  ngOnInit(): void {
    this.load(false)
  }

  protected load(more: boolean): void {
    this.loading.set(true)
    this.api.audit(this.search.value, more ? this.next() : null).subscribe({
      next: page => {
        this.loading.set(false)
        this.entries.set(more ? [...this.entries(), ...page.items] : page.items)
        this.next.set(page.nextBefore)
      },
      error: err => {
        this.loading.set(false)
        this.error.set(problemMessage(err))
      },
    })
  }
}
