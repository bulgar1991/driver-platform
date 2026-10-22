import { NavItem } from '@/core/models'

export const ADMIN_NAVIGATION: NavItem[] = [
  { label: $localize`Dashboard`, path: '/admin', icon: 'grid' },
  { label: $localize`Bookings`, path: '/admin/bookings', icon: 'calendar' },
  { label: $localize`Shifts`, path: '/admin/shifts', icon: 'route' },
  { label: $localize`Drivers`, path: '/admin/drivers', icon: 'users' },
  { label: $localize`Vehicles`, path: '/admin/vehicles', icon: 'car' },
  { label: $localize`Applications`, path: '/admin/applications', icon: 'mail' },
  { label: $localize`Reviews`, path: '/admin/reviews', icon: 'star' },
  {
    label: $localize`Zones`,
    path: '/admin/zones',
    icon: 'map',
    roles: ['Admin'],
  },
  {
    label: $localize`Pricing`,
    path: '/admin/pricing',
    icon: 'tag',
    roles: ['Admin'],
  },
  {
    label: $localize`Payments`,
    path: '/admin/payments',
    icon: 'card',
    roles: ['Admin'],
  },
  {
    label: $localize`Partners`,
    path: '/admin/partners',
    icon: 'briefcase',
    roles: ['Admin'],
  },
  {
    label: $localize`Staff`,
    path: '/admin/staff',
    icon: 'users',
    roles: ['Admin'],
  },
  {
    label: $localize`Audit log`,
    path: '/admin/audit',
    icon: 'lock',
    roles: ['Admin'],
  },
]
