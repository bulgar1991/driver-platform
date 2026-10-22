import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Dialog } from 'primeng/dialog'
import { finalize } from 'rxjs'
import { Partner, PartnerKind, WebhookDelivery } from '@/core/models'
import { AdminPartnersService } from '@/core/services/admin-partners.service'
import { saveBlob } from '@/core/utils/download'
import { todayInItaly } from '@/core/utils/italy-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

/** Business plan §6: companies and Booking that send bookings through the API. */
@Component({
  selector: 'app-partners',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    Dialog,
    ButtonComponent,
    FormFieldComponent,
    IconComponent,
  ],
  templateUrl: './partners.component.html',
  styleUrl: './partners.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartnersComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    newPartner: $localize`New partner`,
    httpsOnly: $localize`Use an https:// address`,
  }

  protected readonly api = inject(AdminPartnersService)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly partners = signal<Partner[]>([])
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  protected readonly editing = signal<Partner | null | undefined>(undefined)
  protected readonly saving = signal(false)
  protected readonly formError = signal<string | null>(null)

  /** Integration panel of the partner being edited. */
  protected readonly newKey = signal<string | null>(null)
  protected readonly secret = signal<string | null>(null)
  protected readonly deliveries = signal<WebhookDelivery[]>([])
  protected readonly notice = signal<string | null>(null)
  protected statementMonth = todayInItaly().slice(0, 7)

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    kind: this.fb.control<PartnerKind>('Company'),
    contactEmail: ['', [Validators.required, Validators.email]],
    priceAdjustmentPercent: [0, [Validators.min(-50), Validators.max(50)]],
    commissionPercent: [0, [Validators.min(0), Validators.max(50)]],
    monthlyInvoicing: [true],
    notifyPassengers: [true],
    webhookUrl: ['', Validators.pattern(/^(https:\/\/|http:\/\/localhost).+/)],
    isActive: [true],
  })

  ngOnInit(): void {
    this.load()
  }

  protected open(partner: Partner | null): void {
    this.formError.set(null)
    this.newKey.set(null)
    this.secret.set(null)
    this.notice.set(null)
    this.deliveries.set([])
    this.form.reset({
      name: partner?.name ?? '',
      kind: partner?.kind ?? 'Company',
      contactEmail: partner?.contactEmail ?? '',
      priceAdjustmentPercent: partner?.priceAdjustmentPercent ?? 0,
      commissionPercent: partner?.commissionPercent ?? 0,
      monthlyInvoicing: partner?.monthlyInvoicing ?? true,
      notifyPassengers: partner?.notifyPassengers ?? true,
      webhookUrl: partner?.webhookUrl ?? '',
      isActive: partner?.isActive ?? true,
    })
    this.editing.set(partner)
    if (partner?.webhookUrl) {
      this.api
        .deliveries(partner.id)
        .subscribe({ next: d => this.deliveries.set(d) })
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    this.saving.set(true)
    this.formError.set(null)
    this.api
      .save(
        { ...v, name: v.name.trim(), webhookUrl: v.webhookUrl.trim() || null },
        this.editing()?.id
      )
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: saved => {
          this.load()
          // A new partner stays open so the API key can be created right away.
          if (!this.editing()) {
            this.editing.set(saved)
            this.notice.set(
              $localize`Partner saved. Create the API key below and send it to their developers.`
            )
          } else {
            this.editing.set(undefined)
          }
        },
        error: err => this.formError.set(problemMessage(err)),
      })
  }

  protected createKey(): void {
    const partner = this.editing()
    if (!partner) {
      return
    }
    this.api.createKey(partner.id).subscribe({
      next: key => {
        this.newKey.set(key.apiKey)
        this.notice.set(
          partner.apiKeyPrefix
            ? $localize`New key created; the old one no longer works.`
            : $localize`Key created.`
        )
        this.load()
      },
      error: err => this.notice.set(problemMessage(err)),
    })
  }

  protected revokeKey(): void {
    const partner = this.editing()
    if (partner) {
      this.api.revokeKey(partner.id).subscribe({
        next: () => {
          this.newKey.set(null)
          this.notice.set(
            $localize`Key revoked: the partner can no longer call the API.`
          )
          this.editing.set({ ...partner, apiKeyPrefix: null })
          this.load()
        },
        error: err => this.notice.set(problemMessage(err)),
      })
    }
  }

  protected showSecret(rotate = false): void {
    const partner = this.editing()
    if (partner) {
      this.api.webhookSecret(partner.id, rotate).subscribe({
        next: r => {
          this.secret.set(r.secret)
          if (rotate) {
            this.notice.set(
              $localize`New webhook secret: the partner must update it on their side.`
            )
          }
        },
        error: err => this.notice.set(problemMessage(err)),
      })
    }
  }

  protected copy(value: string): void {
    void navigator.clipboard?.writeText(value).then(
      () => this.notice.set($localize`Copied.`),
      () => this.notice.set($localize`Select the text and copy it.`)
    )
  }

  protected downloadStatement(): void {
    const partner = this.editing()
    const [year, month] = this.statementMonth.split('-').map(Number)
    if (partner && year && month) {
      this.api.statement(partner.id, year, month).subscribe({
        next: blob =>
          saveBlob(
            blob,
            `statement-${partner.name}-${this.statementMonth}.csv`
          ),
        error: err => this.notice.set(problemMessage(err)),
      })
    }
  }

  private load(): void {
    this.loading.set(true)
    this.api
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: list => this.partners.set(list),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
