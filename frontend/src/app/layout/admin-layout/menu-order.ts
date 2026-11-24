import { NavItem } from '@/core/models'

/**
 * The menu in the user's order: the pages they placed first, in their order; pages they never
 * placed (added to the panel later) keep their built-in place after the one before them.
 */
export function orderMenu(
  items: NavItem[],
  order: string[] | null | undefined
): NavItem[] {
  if (!order?.length) return items
  const rank = new Map(order.map((path, i) => [path, i]))
  const placed = items
    .filter(i => rank.has(i.path))
    .sort((a, b) => rank.get(a.path)! - rank.get(b.path)!)
  const result = [...placed]
  items.forEach((item, i) => {
    if (rank.has(item.path)) return
    // After the built-in item just before it, wherever the user moved that one.
    const before = items
      .slice(0, i)
      .reverse()
      .find(b => result.includes(b))
    result.splice(before ? result.indexOf(before) + 1 : 0, 0, item)
  })
  return result
}

/** Moves an item up (-1) or down (+1); a copy of the list. */
export function moveItem<T>(list: T[], index: number, by: number): T[] {
  const to = index + by
  if (to < 0 || to >= list.length) return list
  const copy = [...list]
  const [item] = copy.splice(index, 1)
  copy.splice(to, 0, item)
  return copy
}
