import {
  DndContext,
  DragOverlay,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragMoveEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { useMemo, useState } from 'react'
import { useCatalogTree, useReorderPages } from '../../../api/catalog'
import {
  arrayMove,
  buildOrderEntries,
  flattenTree,
  getDescendantIds,
  getProjection,
  type FlattenedPage,
} from '../dndTreeUtils'
import { SortableTreeItem } from './SortableTreeItem'

interface Props {
  selectedId: number | null
  onSelect: (id: number) => void
}

export function PageTree({ selectedId, onSelect }: Props) {
  const { data, isLoading, isError } = useCatalogTree()
  const reorderPages = useReorderPages()

  const [activeId, setActiveId] = useState<number | null>(null)
  const [overId, setOverId] = useState<number | null>(null)
  const [offsetLeft, setOffsetLeft] = useState(0)

  const items = useMemo(() => flattenTree(data ?? []), [data])

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
  )

  const activeItem = activeId !== null ? items.find((item) => item.id === activeId) : undefined

  const descendantIds = useMemo(
    () => (activeId !== null ? getDescendantIds(items, activeId) : new Set<number>()),
    [items, activeId],
  )

  const visibleItems = useMemo(
    () => items.filter((item) => !descendantIds.has(item.id)),
    [items, descendantIds],
  )

  const projection =
    activeId !== null && overId !== null
      ? getProjection(visibleItems, activeId, overId, offsetLeft)
      : null

  function handleDragStart(event: DragStartEvent) {
    setActiveId(event.active.id as number)
    setOverId(event.active.id as number)
  }

  function handleDragMove(event: DragMoveEvent) {
    setOffsetLeft(event.delta.x)
  }

  function handleDragOver(event: DragEndEvent) {
    setOverId((event.over?.id as number) ?? null)
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event

    setActiveId(null)
    setOverId(null)
    setOffsetLeft(0)

    if (!over || active.id === over.id) return

    const activeIndex = visibleItems.findIndex((item) => item.id === active.id)
    const overIndex = visibleItems.findIndex((item) => item.id === over.id)

    if (activeIndex === -1 || overIndex === -1) return

    const proj = getProjection(visibleItems, active.id as number, over.id as number, offsetLeft)
    const reorderedVisible = arrayMove(visibleItems, activeIndex, overIndex)

    const movedItem: FlattenedPage = {
      ...reorderedVisible[overIndex],
      parentId: proj.parentId,
      depth: proj.depth,
    }
    reorderedVisible[overIndex] = movedItem

    const originalDescendants = items.filter((item) => descendantIds.has(item.id))
    const insertAt = reorderedVisible.findIndex((item) => item.id === movedItem.id) + 1
    const finalItems = [
      ...reorderedVisible.slice(0, insertAt),
      ...originalDescendants,
      ...reorderedVisible.slice(insertAt),
    ]

    reorderPages.mutate(buildOrderEntries(finalItems))
  }

  if (isLoading) return <p className="p-4 text-sm text-slate-400">Loading…</p>
  if (isError) return <p className="p-4 text-sm text-red-400">Failed to load catalog.</p>

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragStart={handleDragStart}
      onDragMove={handleDragMove}
      onDragOver={handleDragOver}
      onDragEnd={handleDragEnd}
      onDragCancel={() => {
        setActiveId(null)
        setOverId(null)
        setOffsetLeft(0)
      }}
    >
      <SortableContext
        items={visibleItems.map((item) => item.id)}
        strategy={verticalListSortingStrategy}
      >
        <div className="flex flex-col gap-0.5 p-2">
          {visibleItems.map((item) => (
            <SortableTreeItem
              key={item.id}
              item={item}
              isSelected={item.id === selectedId}
              onSelect={onSelect}
              ghostDepth={item.id === overId && projection ? projection.depth : undefined}
            />
          ))}
          {visibleItems.length === 0 && (
            <p className="p-4 text-center text-sm text-slate-500">
              No catalog pages yet. Create one to get started.
            </p>
          )}
        </div>
      </SortableContext>

      <DragOverlay>
        {activeItem ? (
          <div className="rounded-md bg-slate-800 px-3 py-1.5 text-sm text-slate-100 shadow-lg ring-1 ring-indigo-500/50">
            {activeItem.name || activeItem.localization}
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  )
}
