import { Routes } from '@angular/router'
import { roleGuard } from '@/core/guards/role.guard'
import { DashboardComponent } from './dashboard/dashboard.component'
import { BRAND } from '@/core/brand'

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    component: DashboardComponent,
    title: $localize`Dashboard · ${BRAND.name}:brand:`,
  },
  {
    path: 'bookings',
    title: $localize`Bookings · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./bookings/bookings.component').then(m => m.BookingsComponent),
  },
  {
    path: 'calendar',
    title: $localize`Calendar · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./calendar/calendar.component').then(m => m.CalendarComponent),
  },
  {
    path: 'search',
    title: $localize`Search · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./search/search.component').then(m => m.SearchComponent),
  },
  {
    path: 'shifts',
    title: $localize`Shifts · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./shifts/shifts.component').then(m => m.ShiftsComponent),
  },
  {
    path: 'drivers',
    title: $localize`Drivers · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./drivers/drivers.component').then(m => m.DriversComponent),
  },
  {
    path: 'drivers/:id/rides',
    title: $localize`Ride history · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./driver-rides/driver-rides.component').then(
        m => m.DriverRidesComponent
      ),
  },
  {
    path: 'customers',
    title: $localize`Customers · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./customers/customers.component').then(m => m.CustomersComponent),
  },
  {
    path: 'statistics',
    title: $localize`Statistics · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./statistics/statistics.component').then(
        m => m.StatisticsComponent
      ),
  },
  {
    path: 'messages',
    title: $localize`Sent messages · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./messages/messages.component').then(m => m.MessagesComponent),
  },
  {
    path: 'expirations',
    title: $localize`Expirations · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./expirations/expirations.component').then(
        m => m.ExpirationsComponent
      ),
  },
  {
    path: 'attendance',
    title: $localize`Attendance · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./attendance/attendance.component').then(
        m => m.AttendanceComponent
      ),
  },
  {
    path: 'attendance/statistics',
    title: $localize`Attendance statistics · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./attendance/attendance-stats.component').then(
        m => m.AttendanceStatsComponent
      ),
  },
  {
    path: 'driver-pay',
    canActivate: [roleGuard('Admin')],
    title: $localize`Driver pay · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./driver-pay/driver-pay.component').then(
        m => m.DriverPayComponent
      ),
  },
  {
    path: 'vehicles',
    title: $localize`Vehicles · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./vehicles/vehicles.component').then(m => m.VehiclesComponent),
  },
  {
    path: 'zones',
    canActivate: [roleGuard('Admin')],
    title: $localize`Zones · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./zones/zones.component').then(m => m.ZonesComponent),
  },
  {
    path: 'pricing',
    canActivate: [roleGuard('Admin')],
    title: $localize`Pricing · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./pricing/pricing.component').then(m => m.PricingComponent),
  },
  {
    path: 'visitors',
    canActivate: [roleGuard('Admin')],
    title: $localize`Visitors · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./visitors/visitors.component').then(m => m.VisitorsComponent),
  },
  {
    path: 'content',
    canActivate: [roleGuard('Admin')],
    title: $localize`Website content · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./content/content.component').then(m => m.ContentComponent),
  },
  {
    path: 'payments',
    canActivate: [roleGuard('Admin')],
    title: $localize`Payments · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./payments/payments.component').then(m => m.PaymentsComponent),
  },
  {
    path: 'reviews',
    title: $localize`Reviews · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./reviews/reviews.component').then(m => m.ReviewsComponent),
  },
  {
    path: 'partners',
    canActivate: [roleGuard('Admin')],
    title: $localize`Partners · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./partners/partners.component').then(m => m.PartnersComponent),
  },
  {
    path: 'account',
    title: $localize`My account · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./account/account.component').then(m => m.AccountComponent),
  },
  {
    path: 'staff',
    canActivate: [roleGuard('Admin')],
    title: $localize`Staff · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./staff/staff.component').then(m => m.StaffComponent),
  },
  {
    path: 'audit',
    canActivate: [roleGuard('Admin')],
    title: $localize`Audit log · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./audit/audit.component').then(m => m.AuditComponent),
  },
  {
    path: 'applications',
    title: $localize`Driver applications · ${BRAND.name}:brand:`,
    loadComponent: () =>
      import('./applications/applications.component').then(
        m => m.ApplicationsComponent
      ),
  },
]
