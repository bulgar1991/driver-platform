import { Routes } from '@angular/router'
import { roleGuard } from '@/core/guards/role.guard'
import { DashboardComponent } from './dashboard/dashboard.component'

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: DashboardComponent,
    title: 'Dashboard · Magnus Drive',
  },
  {
    path: 'bookings',
    title: 'Bookings · Magnus Drive',
    loadComponent: () =>
      import('./bookings/bookings.component').then(m => m.BookingsComponent),
  },
  {
    path: 'shifts',
    title: 'Shifts · Magnus Drive',
    loadComponent: () =>
      import('./shifts/shifts.component').then(m => m.ShiftsComponent),
  },
  {
    path: 'drivers',
    title: 'Drivers · Magnus Drive',
    loadComponent: () =>
      import('./drivers/drivers.component').then(m => m.DriversComponent),
  },
  {
    path: 'vehicles',
    title: 'Vehicles · Magnus Drive',
    loadComponent: () =>
      import('./vehicles/vehicles.component').then(m => m.VehiclesComponent),
  },
  {
    path: 'zones',
    canActivate: [roleGuard('Admin')],
    title: 'Zones · Magnus Drive',
    loadComponent: () =>
      import('./zones/zones.component').then(m => m.ZonesComponent),
  },
  {
    path: 'pricing',
    canActivate: [roleGuard('Admin')],
    title: 'Pricing · Magnus Drive',
    loadComponent: () =>
      import('./pricing/pricing.component').then(m => m.PricingComponent),
  },
  {
    path: 'payments',
    canActivate: [roleGuard('Admin')],
    title: 'Payments · Magnus Drive',
    loadComponent: () =>
      import('./payments/payments.component').then(m => m.PaymentsComponent),
  },
  {
    path: 'reviews',
    title: 'Reviews · Magnus Drive',
    loadComponent: () =>
      import('./reviews/reviews.component').then(m => m.ReviewsComponent),
  },
  {
    path: 'partners',
    canActivate: [roleGuard('Admin')],
    title: 'Partners · Magnus Drive',
    loadComponent: () =>
      import('./partners/partners.component').then(m => m.PartnersComponent),
  },
  {
    path: 'account',
    title: 'My account · Magnus Drive',
    loadComponent: () =>
      import('./account/account.component').then(m => m.AccountComponent),
  },
  {
    path: 'staff',
    canActivate: [roleGuard('Admin')],
    title: 'Staff · Magnus Drive',
    loadComponent: () =>
      import('./staff/staff.component').then(m => m.StaffComponent),
  },
  {
    path: 'audit',
    canActivate: [roleGuard('Admin')],
    title: 'Audit log · Magnus Drive',
    loadComponent: () =>
      import('./audit/audit.component').then(m => m.AuditComponent),
  },
]
