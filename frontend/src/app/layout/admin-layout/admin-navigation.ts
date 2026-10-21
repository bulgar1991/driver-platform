import { NavItem } from '@/core/models'

export const ADMIN_NAVIGATION: NavItem[] = [
  { label: 'Dashboard', path: '/admin', icon: 'grid' },
  { label: 'Bookings', path: '/admin/bookings', icon: 'calendar' },
  { label: 'Shifts', path: '/admin/shifts', icon: 'route' },
  { label: 'Drivers', path: '/admin/drivers', icon: 'users' },
  { label: 'Vehicles', path: '/admin/vehicles', icon: 'car' },
  { label: 'Reviews', path: '/admin/reviews', icon: 'star' },
  { label: 'Zones', path: '/admin/zones', icon: 'map', roles: ['Admin'] },
  { label: 'Pricing', path: '/admin/pricing', icon: 'tag', roles: ['Admin'] },
  {
    label: 'Payments',
    path: '/admin/payments',
    icon: 'card',
    roles: ['Admin'],
  },
  {
    label: 'Partners',
    path: '/admin/partners',
    icon: 'briefcase',
    roles: ['Admin'],
  },
  { label: 'Staff', path: '/admin/staff', icon: 'users', roles: ['Admin'] },
  { label: 'Audit log', path: '/admin/audit', icon: 'lock', roles: ['Admin'] },
]
