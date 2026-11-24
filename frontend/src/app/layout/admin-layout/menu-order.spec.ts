import { NavItem } from '@/core/models'
import { moveItem, orderMenu } from './menu-order'

const item = (path: string): NavItem => ({ label: path, path, icon: 'grid' })
const paths = (items: NavItem[]) => items.map(i => i.path)
const menu = ['/admin', '/admin/a', '/admin/b', '/admin/c'].map(item)

describe('orderMenu', () => {
  it('keeps the built-in order when the user has none', () => {
    expect(paths(orderMenu(menu, []))).toEqual(paths(menu))
    expect(paths(orderMenu(menu, null))).toEqual(paths(menu))
  })

  it('follows the saved order', () => {
    expect(
      paths(orderMenu(menu, ['/admin/c', '/admin', '/admin/b', '/admin/a']))
    ).toEqual(['/admin/c', '/admin', '/admin/b', '/admin/a'])
  })

  it('puts a page added later after the one before it, and ignores pages that are gone', () => {
    expect(
      paths(orderMenu(menu, ['/admin/c', '/admin/gone', '/admin', '/admin/b']))
    ).toEqual(['/admin/c', '/admin', '/admin/a', '/admin/b'])
  })
})

describe('moveItem', () => {
  it('moves up and down, and stays put at the ends', () => {
    expect(moveItem([1, 2, 3], 1, -1)).toEqual([2, 1, 3])
    expect(moveItem([1, 2, 3], 1, 1)).toEqual([1, 3, 2])
    expect(moveItem([1, 2, 3], 0, -1)).toEqual([1, 2, 3])
    expect(moveItem([1, 2, 3], 2, 1)).toEqual([1, 2, 3])
  })
})
