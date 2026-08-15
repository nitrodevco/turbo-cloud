import type { CatalogPageTreeItem } from '../../api/catalog'

export const INDENTATION_WIDTH = 24

export interface FlattenedPage {
  id: number
  parentId: number | null
  localization: string
  name: string | null
  icon: number
  layout: string
  visible: boolean
  offerCount: number
  depth: number
}

function flatten(
  items: CatalogPageTreeItem[],
  parentId: number | null,
  depth: number,
): FlattenedPage[] {
  return items.reduce<FlattenedPage[]>((acc, item) => {
    acc.push({
      id: item.id,
      parentId,
      localization: item.localization,
      name: item.name,
      icon: item.icon,
      layout: item.layout,
      visible: item.visible,
      offerCount: item.offerCount,
      depth,
    })
    acc.push(...flatten(item.children, item.id, depth + 1))
    return acc
  }, [])
}

export function flattenTree(items: CatalogPageTreeItem[]): FlattenedPage[] {
  return flatten(items, null, 0)
}

/** Rebuilds parent/child order from a flat list, in the list's own order. */
export function buildOrderEntries(
  items: FlattenedPage[],
): { pageId: number; parentId: number | null; sortOrder: number }[] {
  const counters = new Map<number | null, number>()

  return items.map((item) => {
    const key = item.parentId
    const sortOrder = counters.get(key) ?? 0
    counters.set(key, sortOrder + 1)
    return { pageId: item.id, parentId: item.parentId, sortOrder }
  })
}

export function getDescendantIds(items: FlattenedPage[], id: number): Set<number> {
  const result = new Set<number>()
  const stack = [id]

  while (stack.length > 0) {
    const current = stack.pop()!
    for (const item of items) {
      if (item.parentId === current && !result.has(item.id)) {
        result.add(item.id)
        stack.push(item.id)
      }
    }
  }

  return result
}

export function arrayMove<T>(array: T[], from: number, to: number): T[] {
  const copy = array.slice()
  const [moved] = copy.splice(from, 1)
  copy.splice(to, 0, moved)
  return copy
}

interface Projection {
  depth: number
  parentId: number | null
}

export function getProjection(
  items: FlattenedPage[],
  activeId: number,
  overId: number,
  dragOffset: number,
  indentationWidth: number = INDENTATION_WIDTH,
): Projection {
  const overItemIndex = items.findIndex((item) => item.id === overId)
  const activeItemIndex = items.findIndex((item) => item.id === activeId)
  const activeItem = items[activeItemIndex]
  const newItems = arrayMove(items, activeItemIndex, overItemIndex)
  const previousItem = newItems[overItemIndex - 1] as FlattenedPage | undefined
  const nextItem = newItems[overItemIndex + 1] as FlattenedPage | undefined
  const dragDepth = Math.round(dragOffset / indentationWidth)
  const projectedDepth = activeItem.depth + dragDepth
  const maxDepth = previousItem ? previousItem.depth + 1 : 0
  const minDepth = nextItem ? nextItem.depth : 0

  let depth = projectedDepth
  if (projectedDepth >= maxDepth) depth = maxDepth
  else if (projectedDepth < minDepth) depth = minDepth

  return { depth, parentId: getParentId() }

  function getParentId(): number | null {
    if (depth === 0 || !previousItem) return null
    if (depth === previousItem.depth) return previousItem.parentId
    if (depth > previousItem.depth) return previousItem.id

    const newParent = newItems
      .slice(0, overItemIndex)
      .reverse()
      .find((item) => item.depth === depth)?.parentId

    return newParent ?? null
  }
}
