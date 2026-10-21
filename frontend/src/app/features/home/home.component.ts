import { ChangeDetectionStrategy, Component } from '@angular/core'
import { DriversJoinComponent } from './components/drivers-join/drivers-join.component'
import { HeroComponent } from './components/hero/hero.component'
import { PartnersComponent } from './components/partners/partners.component'
import { ServicesSectionComponent } from './components/services-section/services-section.component'
import { TariffsComponent } from './components/tariffs/tariffs.component'
import { TestimonialsComponent } from './components/testimonials/testimonials.component'
import { WelcomeComponent } from './components/welcome/welcome.component'

/** The public home page; each section is its own component, texts live in home.content.ts. */
@Component({
  selector: 'app-home',
  imports: [
    HeroComponent,
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
