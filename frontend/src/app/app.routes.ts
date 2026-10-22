import { Routes } from '@angular/router'
import { PageSeo } from './core/models'
import { authGuard } from './core/guards/auth.guard'
import { guestGuard } from './core/guards/guest.guard'
import { localePrefixGuard } from './core/guards/locale-prefix.guard'
import { passwordChangeGuard } from './core/guards/password-change.guard'
import { PublicLayoutComponent } from './layout/public-layout/public-layout.component'

/** Route data for SeoService. Routes without it are marked noindex (panel, personal links…). */
const seo = (
  description: string,
  schema?: PageSeo['schema']
): { seo: PageSeo } => ({
  seo: { description, schema },
})

export const routes: Routes = [
  {
    path: '',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        title: $localize`Magnus Drive · Private Driver Services`,
        loadComponent: () =>
          import('./features/home/home.component').then(m => m.HomeComponent),
        data: seo(
          $localize`Private transfers with driver in Italy, France, Switzerland and Germany: airports, stations, ports and cities at a fixed price. Your driver meets you on arrival.`,
          'business'
        ),
      },
      {
        path: 'book',
        title: $localize`Book a ride · Magnus Drive`,
        loadComponent: () =>
          import('./features/booking/booking.component').then(
            m => m.BookingComponent
          ),
        data: seo(
          $localize`Book a private transfer in Italy, France, Switzerland or Germany: enter from and to, see the route and the fixed price, pay by card or PayPal.`
        ),
      },
      {
        path: 'services',
        title: $localize`Our services · Magnus Drive`,
        loadComponent: () =>
          import('./features/home/components/services-section/services-section.component').then(
            m => m.ServicesSectionComponent
          ),
        data: {
          page: true,
          ...seo(
            $localize`Airport and station transfers, business travel, city-to-city rides, Venice, groups and families: chauffeured services across Italy and its neighbours at a fixed price.`
          ),
        },
      },
      {
        path: 'tariffs',
        title: $localize`Tariffs · Magnus Drive`,
        loadComponent: () =>
          import('./features/home/components/tariffs/tariffs.component').then(
            m => m.TariffsComponent
          ),
        data: {
          page: true,
          ...seo(
            $localize`Transfer prices: base fare and price per km for sedan, SUV and van, night and holiday surcharges, return discount. VAT included, no hidden fees.`
          ),
        },
      },
      {
        path: 'drivers',
        title: $localize`For drivers · Magnus Drive`,
        loadComponent: () =>
          import('./features/home/components/drivers-join/drivers-join.component').then(
            m => m.DriversJoinComponent
          ),
        data: {
          page: true,
          ...seo(
            $localize`Licensed NCC driver in Italy? Drive with Magnus Drive: rides sent to your phone, commission agreed upfront, the area you choose. Apply in two minutes.`
          ),
        },
      },
      {
        path: 'reviews',
        title: $localize`Reviews · Magnus Drive`,
        loadComponent: () =>
          import('./features/home/components/testimonials/testimonials.component').then(
            m => m.TestimonialsComponent
          ),
        data: {
          page: true,
          ...seo(
            $localize`What clients say about their Magnus Drive transfers: reviews from real rides, published with their consent.`
          ),
        },
      },
      {
        path: 'partners',
        title: $localize`Partners · Magnus Drive`,
        loadComponent: () =>
          import('./features/home/components/partners/partners.component').then(
            m => m.PartnersComponent
          ),
        data: {
          page: true,
          ...seo(
            $localize`Hotels, travel agencies and businesses work with Magnus Drive: partner rates, a booking API and monthly invoicing for transfers in Italy and Europe.`
          ),
        },
      },
      {
        // Signed link from the confirmation: pay, check the status, download the invoice.
        path: 'my-booking/:reference/:token',
        title: $localize`Your booking · Magnus Drive`,
        loadComponent: () =>
          import('./features/my-booking/my-booking.component').then(
            m => m.MyBookingComponent
          ),
      },
      {
        path: 'privacy',
        title: $localize`Privacy policy · Magnus Drive`,
        loadComponent: () =>
          import('./features/legal/legal.component').then(
            m => m.LegalComponent
          ),
        data: {
          page: 'privacy',
          ...seo(
            $localize`How Magnus Drive collects, uses and protects your personal data when you book a transfer, under the GDPR.`
          ),
        },
      },
      {
        path: 'terms',
        title: $localize`Terms of service · Magnus Drive`,
        loadComponent: () =>
          import('./features/legal/legal.component').then(
            m => m.LegalComponent
          ),
        data: {
          page: 'terms',
          ...seo(
            $localize`Terms for booking private transfers with Magnus Drive: how bookings, fixed prices and payment work, and what each side is responsible for.`
          ),
        },
      },
      {
        // One-time review link sent after the ride.
        path: 'review/:token',
        title: $localize`Rate your ride · Magnus Drive`,
        loadComponent: () =>
          import('./features/review/review.component').then(
            m => m.ReviewComponent
          ),
      },
      {
        // Development stand-in for Stripe's payment page.
        path: 'dev/payment/:sessionId',
        title: $localize`Test payment · Magnus Drive`,
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
    // /de/tariffs on the Italian dev server → /tariffs.
    path: '**',
    canMatch: [localePrefixGuard],
    children: [],
  },
  {
    path: '**',
    component: PublicLayoutComponent,
    children: [
      {
        path: '',
        title: $localize`Not found · Magnus Drive`,
        loadComponent: () =>
          import('./features/not-found/not-found.component').then(
            m => m.NotFoundComponent
          ),
      },
    ],
  },
]
