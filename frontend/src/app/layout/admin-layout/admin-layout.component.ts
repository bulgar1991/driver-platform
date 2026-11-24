import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core'
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'
import {
  CdkDrag,
  CdkDragDrop,
  CdkDragHandle,
  CdkDropList,
  moveItemInArray,
} from '@angular/cdk/drag-drop'
import { finalize } from 'rxjs'
import { NavItem } from '@/core/models'
import { ToastService } from '@/core/services/toast.service'
import { problemMessage } from '@/core/utils/problem'
import { moveItem, orderMenu } from './menu-order'
import { AuthService } from '@/core/services/auth.service'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { LogoComponent } from '@/shared/components/logo/logo.component'
import { StaffRole } from '@/core/models'
import { LanguageSwitchComponent } from '../language-switch/language-switch.component'
import { ADMIN_NAVIGATION } from './admin-navigation'
import { VehicleClassService } from '@/core/services/vehicle-class.service'

@Component({
  selector: 'app-admin-layout',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    IconComponent,
    LanguageSwitchComponent,
    LogoComponent,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
  ],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLayoutComponent {
  private readonly auth = inject(AuthService)

  constructor() {
    // Tariff names for every page of the panel (cars, zones, bookings, shifts).
    inject(VehicleClassService)
      .load()
      .subscribe({ error: () => undefined })
  }

  protected readonly user = this.auth.currentUser
  protected readonly menuOpen = signal(false)
  protected readonly roleLabels: Record<StaffRole, string> = {
    Admin: $localize`Admin`,
    Dispatcher: $localize`Dispatcher`,
  }
  /** The pages this user may open, in their own order. */
  protected readonly navigation = computed(() =>
    orderMenu(
      ADMIN_NAVIGATION.filter(
        item => !item.roles || this.auth.hasRole(...item.roles)
      ),
      this.user()?.menuOrder
    )
  )

  // ── Edit menu: drag the items (or the arrows), then save ───────────────────
  private readonly toast = inject(ToastService)
  /** The list being arranged; null when not editing. */
  protected readonly draft = signal<NavItem[] | null>(null)
  protected readonly saving = signal(false)

  protected editMenu(): void {
    this.draft.set([...this.navigation()])
  }

  protected cancelEdit(): void {
    this.draft.set(null)
  }

  protected drop(event: CdkDragDrop<NavItem[]>): void {
    const list = [...(this.draft() ?? [])]
    moveItemInArray(list, event.previousIndex, event.currentIndex)
    this.draft.set(list)
  }

  protected move(index: number, by: number): void {
    this.draft.update(list => (list ? moveItem(list, index, by) : list))
  }

  /** Back to the built-in order (saved at once). */
  protected resetMenu(): void {
    this.save([], $localize`Menu back to the original order.`)
  }

  protected saveMenu(): void {
    this.save(
      (this.draft() ?? []).map(i => i.path),
      $localize`Menu order saved.`
    )
  }

  private save(order: string[], message: string): void {
    this.saving.set(true)
    this.auth
      .setMenuOrder(order)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.draft.set(null)
          this.toast.success(message)
        },
        error: err => this.toast.error(problemMessage(err)),
      })
  }

  protected toggleMenu(): void {
    this.menuOpen.update(open => !open)
  }

  protected logout(): void {
    this.auth.logout()
  }
}
