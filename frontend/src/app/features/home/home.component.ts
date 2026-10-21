import { ChangeDetectionStrategy, Component } from '@angular/core'
import { ServiceOffering } from '@/core/models'
import { SectionHeadingComponent } from '@/shared/components/section-heading/section-heading.component'
import { ServiceCardComponent } from '@/shared/components/service-card/service-card.component'
import { HeroComponent } from './components/hero/hero.component'

@Component({
  selector: 'app-home',
  imports: [HeroComponent, SectionHeadingComponent, ServiceCardComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent {
  protected readonly services: ServiceOffering[] = [
    {
      title: 'Airport Transfers',
      description: 'Meet & greet in arrivals, flight tracked, fixed price.',
      icon: 'plane',
    },
    {
      title: 'Business Travel',
      description: 'Discreet chauffeurs and monthly invoicing for companies.',
      icon: 'briefcase',
    },
    {
      title: 'Stations & Ports',
      description: 'Train stations and cruise terminals across Italy.',
      icon: 'pin',
    },
    {
      title: 'Premium Service',
      description: 'Child seats, name sign and luggage space on request.',
      icon: 'diamond',
    },
  ]
}
