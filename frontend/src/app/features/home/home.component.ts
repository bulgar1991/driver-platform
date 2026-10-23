import { ChangeDetectionStrategy, Component } from '@angular/core'
import { BookingComponent } from '../booking/booking.component'
import { DriversJoinComponent } from './components/drivers-join/drivers-join.component'
import { PartnersComponent } from './components/partners/partners.component'
import { ServicesSectionComponent } from './components/services-section/services-section.component'
import { TariffsComponent } from './components/tariffs/tariffs.component'
import { TestimonialsComponent } from './components/testimonials/testimonials.component'
import { WelcomeComponent } from './components/welcome/welcome.component'

/**
 * The home page: the booking first (search bar, prices and the map with the route), then the
 * sections that also have their own pages. Texts live in home.content.ts.
 */
@Component({
  selector: 'app-home',
  imports: [
    BookingComponent,
    WelcomeComponent,
    ServicesSectionComponent,
    TariffsComponent,
    DriversJoinComponent,
    TestimonialsComponent,
    PartnersComponent,
  ],
  templateUrl: './home.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent {}
