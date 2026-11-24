import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  LOCALE_ID,
  OnInit,
  signal,
} from '@angular/core'
import { FormsModule } from '@angular/forms'
import { MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { finalize } from 'rxjs'
import {
  ContentText,
  ImageAsset,
  SiteContent,
  VehicleClassNames,
} from '@/core/models'
import { AdminContentService } from '@/core/services/admin-content.service'
import { ToastService } from '@/core/services/toast.service'
import { IMAGE_TYPES, imageUploadError } from '@/core/utils/image-upload'
import { problemMessage } from '@/core/utils/problem'
import {
  EDITABLE_SECTIONS,
  EditableField,
  vehicleGroup,
} from '@/features/home/editable-content'
import { SITE_LOCALES, siteLocale } from '@/core/i18n/locales'
import {
  pickName,
  VehicleClassService,
} from '@/core/services/vehicle-class.service'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ImageComponent } from '@/shared/components/image/image.component'

type Language = keyof ContentText

const EMPTY: ContentText = { en: null, it: null, de: null, fr: null }

/**
 * Admin → Website content: the texts (in the four languages) and pictures of the public site.
 * An empty language shows the site's built-in text; "Use built-in" removes a change.
 */
@Component({
  selector: 'app-content',
  imports: [
    FormsModule,
    MatFormField,
    MatLabel,
    MatInput,
    ButtonComponent,
    IconComponent,
    ImageComponent,
  ],
  templateUrl: './content.component.html',
  styleUrl: './content.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContentComponent implements OnInit {
  private readonly api = inject(AdminContentService)
  private readonly toast = inject(ToastService)

  private readonly vehicleClasses = inject(VehicleClassService)
  /**
   * The sections, with "Cars and tariffs" listing the tariffs offered in Admin → Pricing: a tariff
   * deleted or hidden there is not on the site, so it has nothing to edit here either.
   */
  protected readonly sections = computed(() => {
    const classes = this.vehicleClasses.classes()
    if (!classes.length) return EDITABLE_SECTIONS
    const tariffs = classes
      .filter(c => c.isActive)
      .map(c =>
        vehicleGroup(
          c.category,
          c.isBuiltIn ? null : pickName(c.names, this.language)
        )
      )
    return EDITABLE_SECTIONS.map(s =>
      s.id === 'vehicles' ? { ...s, groups: [s.groups[0], ...tariffs] } : s
    )
  })
  private readonly language = siteLocale(inject(LOCALE_ID))
    .code as keyof VehicleClassNames
  protected readonly languages = SITE_LOCALES.map(l => ({
    code: l.code as Language,
    name: l.name,
  }))
  protected readonly imageTypes = IMAGE_TYPES
  protected readonly tx = {
    saved: $localize`Saved. The website shows it within a minute.`,
    reset: $localize`Back to the built-in content.`,
    uploaded: $localize`Picture saved as WebP.`,
  }

  protected readonly sectionId = signal(EDITABLE_SECTIONS[0].id)
  protected readonly section = computed(
    () =>
      this.sections().find(s => s.id === this.sectionId()) ?? this.sections()[0]
  )
  protected readonly saved = signal<SiteContent>({ texts: {}, images: {} })
  protected readonly loading = signal(true)
  protected readonly busy = signal<string | null>(null)
  protected readonly error = signal<string | null>(null)
  /** What is typed, per key (the inputs are bound to these objects). */
  protected drafts: Record<string, ContentText> = {}

  ngOnInit(): void {
    this.api
      .get()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: content => this.load(content),
        error: err => this.error.set(problemMessage(err)),
      })
  }

  protected draft(key: string): ContentText {
    return (this.drafts[key] ??= { ...(this.saved().texts[key] ?? EMPTY) })
  }

  protected changed(key: string): boolean {
    return !!this.saved().texts[key] || !!this.saved().images[key]
  }

  protected dirty(key: string): boolean {
    const saved = this.saved().texts[key] ?? EMPTY
    const draft = this.draft(key)
    return this.languages.some(
      l => (draft[l.code]?.trim() || null) !== (saved[l.code] || null)
    )
  }

  protected image(field: EditableField): ImageAsset {
    const uploaded = this.saved().images[field.key]
    return uploaded
      ? { src: uploaded.url, width: uploaded.width, height: uploaded.height }
      : (field.builtIn as ImageAsset)
  }

  protected saveText(key: string): void {
    this.run(key, this.api.setText(key, this.draft(key)), this.tx.saved)
  }

  protected resetText(key: string): void {
    this.drafts[key] = { ...EMPTY }
    this.run(key, this.api.setText(key, EMPTY), this.tx.reset)
  }

  protected pickImage(key: string, event: Event): void {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) {
      return
    }
    const error = imageUploadError(file)
    if (error) {
      this.toast.error(error)
      return
    }
    this.run(key, this.api.setImage(key, file), this.tx.uploaded)
  }

  protected removeImage(key: string): void {
    this.run(key, this.api.removeImage(key), this.tx.reset)
  }

  private run(
    key: string,
    request: ReturnType<AdminContentService['get']>,
    done: string
  ): void {
    this.busy.set(key)
    request.pipe(finalize(() => this.busy.set(null))).subscribe({
      next: content => {
        this.saved.set(content)
        delete this.drafts[key]
        this.toast.success(done)
      },
      error: err => this.toast.error(problemMessage(err)),
    })
  }

  private load(content: SiteContent): void {
    this.saved.set(content)
    this.drafts = {}
  }
}
