import { Routes } from '@angular/router'
import { authGuard } from './core/guards/auth.guard'
import { guestGuard } from './core/guards/guest.guard'
import { passwordChangeGuard } from './core/guards/password-change.guard'
import { PublicLayoutComponent } from './layout/public-layout/public-layout.component'

export const routes: Routes = [
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        title: 'Magnus Drive · Private Driver Services',
        loadComponent: () =>
          import('./features/home/home.component').then(m => m.HomeComponent),
      },
      {
        path: 'book',
        title: 'Book a ride · Magnus Drive',
        loadComponent: () =>
          import('./features/booking/booking.component').then(
            m => m.BookingComponent
          ),
      },
      {
        path: 'services',
        title: 'Our services · Magnus Drive',
        loadComponent: () =>
          import('./features/home/components/services-section/services-section.component').then(
            m => m.ServicesSectionComponent
          ),
        data: { page: true },
      },
      {
        path: 'tariffs',
        title: 'Tariffs · Magnus Drive',
        loadComponent: () =>
          import('./features/home/components/tariffs/tariffs.component').then(
            m => m.TariffsComponent
          ),
        data: { page: true },
      },
      {
        path: 'drivers',
        title: 'For drivers · Magnus Drive',
        loadComponent: () =>
          import('./features/home/components/drivers-join/drivers-join.component').then(
            m => m.DriversJoinComponent
          ),
        data: { page: true },
      },
      {
        path: 'reviews',
        title: 'Reviews · Magnus Drive',
        loadComponent: () =>
          import('./features/home/components/testimonials/testimonials.component').then(
            m => m.TestimonialsComponent
          ),
        data: { page: true },
      },
      {
        path: 'partners',
        title: 'Partners · Magnus Drive',
        loadComponent: () =>
          import('./features/home/components/partners/partners.component').then(
            m => m.PartnersComponent
          ),
        data: { page: true },
      },
      {
        // Signed link from the confirmation: pay, check the status, download the invoice.
        path: 'my-booking/:reference/:token',
        title: 'Your booking · Magnus Drive',
        loadComponent: () =>
          import('./features/my-booking/my-booking.component').then(
            m => m.MyBookingComponent
          ),
      },
      {
        path: 'privacy',
        title: 'Privacy policy · Magnus Drive',
        loadComponent: () =>
          import('./features/legal/legal.component').then(
            m => m.LegalComponent
          ),
        data: { page: 'privacy' },
      },
      {
        path: 'terms',
        title: 'Terms of service · Magnus Drive',
        loadComponent: () =>
          import('./features/legal/legal.component').then(
            m => m.LegalComponent
          ),
        data: { page: 'terms' },
      },
      {
        // One-time review link sent after the ride.
        path: 'review/:token',
        title: 'Rate your ride · Magnus Drive',
        loadComponent: () =>
          import('./features/review/review.component').then(
            m => m.ReviewComponent
          ),
      },
      {
        // Development stand-in for Stripe's payment page.
        path: 'dev/payment/:sessionId',
        title: 'Test payment · Magnus Drive',
        loadComponent: () =>
          import('./features/dev-payment/dev-payment.component').then(
            m => m.DevPaymentComponent
          ),
      },
    ],
  },
  {
    path: 'login',
    title: 'Sign in · Magnus Drive',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then(
        m => m.LoginComponent
      ),
  },
  {
    path: 'admin',
    canActivate: [authGuard],
    canActivateChild: [passwordChangeGuard],
    loadComponent: () =>
      import('./layout/admin-layout/admin-layout.component').then(
        m => m.AdminLayoutComponent
      ),
    loadChildren: () =>
      import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
  },
  {
    // Personal driver link with all assigned shifts (step 5).
    path: 'driver/:token',
    title: 'My shifts · Magnus Drive',
    loadComponent: () =>
      import('./features/driver-portal/driver-portal.component').then(
        m => m.DriverPortalComponent
      ),
  },
  {
    path: '**',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        title: 'Not found · Magnus Drive',
        loadComponent: () =>
          import('./features/not-found/not-found.component').then(
            m => m.NotFoundComponent
          ),
      },
    ],
  },
]
