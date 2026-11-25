import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop'
import {
  FormArray,
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms'
import { RouterLink } from '@angular/router'
import {
  MatAutocomplete,
  MatAutocompleteSelectedEvent,
  MatAutocompleteTrigger,
} from '@angular/material/autocomplete'
import { MatCheckbox } from '@angular/material/checkbox'
import {
  MatError,
  MatFormField,
  MatHint,
  MatLabel,
} from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  filter,
  finalize,
  map,
  of,
  switchMap,
} from 'rxjs'
import {
  CustomerListItem,
  GeoPoint,
  LatLng,
  MapMarker,
  OfficeBookingRequest,
  OfficeBookingResult,
  Partner,
  PaymentMethod,
  Quote,
  QuoteRequest,
} from '@/core/models'
import { AdminCustomersService } from '@/core/services/admin-customers.service'
import { AdminDispatchService } from '@/core/services/admin-dispatch.service'
import { AdminPartnersService } from '@/core/services/admin-partners.service'
import { AuthService } from '@/core/services/auth.service'
import { ToastService } from '@/core/services/toast.service'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { PAYMENT_METHOD_LABEL } from '@/core/utils/status-labels'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { DialogService } from '@/shared/components/dialog/dialog.service'
import {
  MapPickerData,
  MapPickerDialogComponent,
} from './map-picker/map-picker-dialog.component'
import { MapComponent } from '@/shared/components/map/map.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { AddressInputComponent } from '@/features/booking/components/address-input/address-input.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

/** The methods offered here, in the order of the list; PayPal is only chosen on Stripe's page. */
const METHODS: PaymentMethod[] = [
  'CashOnBoard',
  'CardOnBoard',
  'Card',
  'CashAtOffice',
  'BankTransfer',
  'PaidByPartner',
  'FollowingInvoice',
  'Other',
]

/**
 * Admin → Bookings → New booking: a ride taken by phone, WhatsApp or email. Route start and end are
 * required; the price comes from the tariffs and can be changed; the payment is cash or card to the
 * driver, a Stripe link for the client, or recorded later (transfer, invoice, partner).
 */
@Component({
  selector: 'app-new-booking',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormField,
    MatLabel,
    MatHint,
    MatError,
    MatInput,
    MatSelect,
    MatOption,
    MatCheckbox,
    MatAutocomplete,
    MatAutocompleteTrigger,
    AddressInputComponent,
    ButtonComponent,
    IconComponent,
    MapComponent,
    MoneyPipe,
  ],
  templateUrl: './new-booking.component.html',
  styleUrl: './new-booking.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewBookingComponent implements OnInit {
  private readonly fb = inject(FormBuilder).nonNullable
  private readonly api = inject(AdminDispatchService)
  private readonly customersApi = inject(AdminCustomersService)
  private readonly partnersApi = inject(AdminPartnersService)
  private readonly toast = inject(ToastService)
  private readonly dialog = inject(DialogService)
  private readonly destroyRef = inject(DestroyRef)
  protected readonly isAdmin = inject(AuthService).hasRole('Admin')

  protected readonly text = ADMIN_TEXT
  protected readonly methods = METHODS
  protected readonly methodLabel = PAYMENT_METHOD_LABEL
  protected readonly methodHint: Partial<Record<PaymentMethod, string>> = {
    CashOnBoard: $localize`The driver collects it at the ride; mark it received afterwards in Payments.`,
    CardOnBoard: $localize`The driver takes the card at the ride; mark it received afterwards in Payments.`,
    Card: $localize`You get a Stripe link to send the client; the booking waits until it is paid.`,
    CashAtOffice: $localize`Mark it received in Payments when the client pays.`,
    BankTransfer: $localize`Mark it received in Payments when the transfer arrives.`,
    PaidByPartner: $localize`The partner collects it; no email to the client.`,
    FollowingInvoice: $localize`Billed later; no email to the client.`,
  }
  protected readonly languages = [
    { code: 'it', label: 'Italiano' },
    { code: 'en', label: 'English' },
    { code: 'de', label: 'Deutsch' },
    { code: 'fr', label: 'Français' },
  ]

  protected readonly form = this.fb.group({
    customerSearch: [''],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: ['', [Validators.required, Validators.maxLength(32)]],
    email: ['', [Validators.email, Validators.maxLength(254)]],
    language: ['it'],
    pickup: this.fb.control<GeoPoint | null>(null, Validators.required),
    dropoff: this.fb.control<GeoPoint | null>(null, Validators.required),
    date: [todayInOffice(), Validators.required],
    time: ['', Validators.required],
    passengers: [
      1,
      [Validators.required, Validators.min(1), Validators.max(50)],
    ],
    largeLuggage: [0, [Validators.min(0), Validators.max(60)]],
    smallLuggage: [0, [Validators.min(0), Validators.max(60)]],
    passengerName: ['', Validators.maxLength(60)],
    nameSign: [false],
    flightNumber: ['', Validators.pattern(/^[A-Za-z0-9 ]{2,16}$/)],
    vehicleCategory: ['', Validators.required],
    price: this.fb.control<number | null>(null, [Validators.min(0)]),
    costs: this.fb.array<ReturnType<NewBookingComponent['costRow']>>([]),
    paymentMethod: this.fb.control<PaymentMethod | ''>('', Validators.required),
    partnerId: [''],
    externalReference: ['', Validators.maxLength(64)],
    driverNotes: ['', Validators.maxLength(2000)],
    internalNotes: ['', Validators.maxLength(2000)],
  })

  /** An existing customer picked from the search; their details are not typed again. */
  protected readonly customer = signal<CustomerListItem | null>(null)
  protected readonly customerResults = signal<CustomerListItem[]>([])
  protected readonly partners = signal<Partner[]>([])

  protected readonly quote = signal<Quote | null>(null)
  /** The route on the map: start and end as soon as they are chosen, the road once priced. */
  protected readonly routeMarkers = computed<MapMarker[]>(() => {
    const v = this.values()
    const markers: MapMarker[] = []
    if (v.pickup)
      markers.push({
        position: [v.pickup.latitude, v.pickup.longitude],
        kind: 'pickup',
      })
    if (v.dropoff)
      markers.push({
        position: [v.dropoff.latitude, v.dropoff.longitude],
        kind: 'dropoff',
      })
    return markers
  })
  protected readonly routePath = computed<LatLng[]>(
    () => this.quote()?.outbound.path ?? []
  )
  protected readonly routeFit = computed<LatLng[]>(() => [
    ...this.routeMarkers().map(m => m.position),
    ...this.routePath(),
  ])
  protected readonly quoting = signal(false)
  protected readonly quoteError = signal<string | null>(null)
  protected readonly saving = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly created = signal<OfficeBookingResult | null>(null)

  private readonly values = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  })

  protected readonly tariff = computed(() => {
    const category = this.values().vehicleCategory
    return this.quote()?.vehicles.find(v => v.category === category) ?? null
  })

  protected readonly costsTotal = computed(() =>
    (this.values().costs ?? []).reduce(
      (sum, c) => sum + (Number(c.amount) || 0),
      0
    )
  )

  protected readonly total = computed(() => {
    const price = this.values().price
    const ride =
      price === null || price === undefined || (price as unknown) === ''
        ? (this.tariff()?.outbound.total ?? 0)
        : Number(price)
    return ride + this.costsTotal()
  })

  protected get costs(): FormArray {
    return this.form.controls.costs
  }

  ngOnInit(): void {
    if (this.isAdmin) {
      this.partnersApi
        .list()
        .pipe(catchError(() => of([])))
        .subscribe(list => this.partners.set(list.filter(p => p.isActive)))
    }

    this.form.controls.customerSearch.valueChanges
      .pipe(
        debounceTime(250),
        map(v => (typeof v === 'string' ? v.trim() : '')),
        distinctUntilChanged(),
        switchMap(q =>
          q.length < 2
            ? of([])
            : this.customersApi
                .list({
                  search: q,
                  filter: 'All',
                  sort: 'Name',
                  page: 1,
                  pageSize: 8,
                })
                .pipe(
                  map(r => r.items),
                  catchError(() => of([]))
                )
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(list => this.customerResults.set(list))

    // A new price whenever the route, the time or the party changes.
    this.form.valueChanges
      .pipe(
        map(() => this.tripRequest()),
        filter((trip): trip is QuoteRequest => trip !== null),
        map(trip => JSON.stringify(trip)),
        distinctUntilChanged(),
        debounceTime(400),
        switchMap(json => {
          this.quoting.set(true)
          this.quoteError.set(null)
          return this.api.quote(JSON.parse(json) as QuoteRequest).pipe(
            catchError(err => {
              this.quoteError.set(problemMessage(err))
              return of(null)
            }),
            finalize(() => this.quoting.set(false))
          )
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(quote => {
        this.quote.set(quote)
        const chosen = this.form.controls.vehicleCategory
        if (quote && !quote.vehicles.some(v => v.category === chosen.value)) {
          const first =
            quote.vehicles.find(v => v.available) ?? quote.vehicles[0]
          chosen.setValue(first?.category ?? '')
        }
      })
  }

  /** Route start or end chosen on a map: the street address of the point clicked. */
  protected pickOnMap(field: 'pickup' | 'dropoff'): void {
    const control = this.form.controls[field]
    this.dialog
      .open<MapPickerDialogComponent, MapPickerData, GeoPoint | undefined>(
        MapPickerDialogComponent,
        {
          width: '56rem',
          data: {
            title:
              field === 'pickup'
                ? $localize`Route start on the map`
                : $localize`Route end on the map`,
            kind: field,
            value: control.value,
          },
        }
      )
      .afterClosed()
      .subscribe(point => {
        if (!point) return
        control.setValue(point)
        control.markAsTouched()
        control.markAsDirty()
      })
  }

  protected customerLabel(c: CustomerListItem | string | null): string {
    return typeof c === 'string' ? c : c ? `${c.name} · ${c.phone}` : ''
  }

  protected pickCustomer(event: MatAutocompleteSelectedEvent): void {
    const c = event.option.value as CustomerListItem
    this.customer.set(c)
    const [first, ...rest] = c.name.split(' ')
    this.form.patchValue({
      firstName: first ?? '',
      lastName: rest.join(' ') || '-',
      phone: c.phone,
      email: c.email ?? '',
      language: c.language,
    })
  }

  protected clearCustomer(): void {
    this.customer.set(null)
    this.form.patchValue({
      customerSearch: '',
      firstName: '',
      lastName: '',
      phone: '',
      email: '',
      language: 'it',
    })
  }

  protected vehicleName(category: string, name: string | null): string {
    return name ?? this.text.categories[category]
  }

  protected addCost(): void {
    this.costs.push(this.costRow())
  }

  protected removeCost(index: number): void {
    this.costs.removeAt(index)
  }

  /** Puts the tariff price in the field, to change it from there. */
  protected useTariff(): void {
    this.form.controls.price.setValue(this.tariff()?.outbound.total ?? null)
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      this.error.set($localize`Fill in the fields marked in red.`)
      return
    }
    const v = this.form.getRawValue()
    const trip = this.tripRequest()!
    const request: OfficeBookingRequest = {
      clientId: this.customer()?.id ?? null,
      customer: this.customer()
        ? null
        : {
            firstName: v.firstName.trim(),
            lastName: v.lastName.trim(),
            phone: v.phone.trim(),
            email: v.email.trim() || null,
            language: v.language,
          },
      trip,
      vehicleCategory: v.vehicleCategory,
      partnerId: v.partnerId || null,
      externalReference: v.externalReference.trim() || null,
      passengerName: v.passengerName.trim() || null,
      flightNumber: v.flightNumber.trim() || null,
      price:
        v.price === null || (v.price as unknown) === ''
          ? null
          : Number(v.price),
      additionalCosts: v.costs
        .filter(c => c.description.trim() && Number(c.amount))
        .map(c => ({
          description: c.description.trim(),
          amount: Number(c.amount),
        })),
      paymentMethod: v.paymentMethod as PaymentMethod,
      driverNotes: v.driverNotes.trim() || null,
      internalNotes: v.internalNotes.trim() || null,
    }

    this.saving.set(true)
    this.error.set(null)
    this.api
      .createBooking(request)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: result => {
          this.created.set(result)
          this.toast.success(
            $localize`Booking ${result.reference}:reference: saved.`
          )
          window.scrollTo({ top: 0 })
        },
        error: err => this.error.set(problemMessage(err)),
      })
  }

  protected copyLink(url: string): void {
    void navigator.clipboard
      ?.writeText(url)
      .then(() => this.toast.success($localize`Payment link copied.`))
  }

  /** Clears the ride, keeps nothing: ready for the next call. */
  protected another(): void {
    this.created.set(null)
    this.customer.set(null)
    this.quote.set(null)
    this.costs.clear()
    this.form.reset()
  }

  private costRow() {
    return this.fb.group({
      description: ['', Validators.maxLength(100)],
      amount: this.fb.control<number | null>(null),
    })
  }

  private tripRequest(): QuoteRequest | null {
    const v = this.form.getRawValue()
    if (!v.pickup || !v.dropoff || !v.date || !v.time) {
      return null
    }
    return {
      pickup: v.pickup,
      dropoff: v.dropoff,
      pickupAt: `${v.date}T${v.time}:00`,
      passengers: Number(v.passengers) || 1,
      largeLuggage: Number(v.largeLuggage) || 0,
      smallLuggage: Number(v.smallLuggage) || 0,
      childSeats: [],
      nameSign: v.nameSign,
    }
  }
}
