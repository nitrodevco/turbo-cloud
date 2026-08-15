import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { INDENTATION_WIDTH, type FlattenedPage } from '../dndTreeUtils'

interface Props {
  item: FlattenedPage
  isSelected: boolean
  onSelect: (id: number) => void
  ghostDepth?: number
}

export function SortableTreeItem({ item, isSelected, onSelect, ghostDepth }: Props) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: item.id,
  })

  const depth = ghostDepth ?? item.depth

  return (
    <div
      ref={setNodeRef}
      style={{
        transform: CSS.Translate.toString(transform),
        transition,
        paddingLeft: depth * INDENTATION_WIDTH,
      }}
      className={`group flex items-center gap-2 rounded-md py-1.5 pr-2 ${
        isDragging ? 'opacity-40' : ''
      } ${isSelected ? 'bg-indigo-500/15' : 'hover:bg-slate-800/60'}`}
    >
      <button
        type="button"
        {...attributes}
        {...listeners}
        className="cursor-grab touch-none rounded px-1 text-slate-600 hover:text-slate-300 active:cursor-grabbing"
        aria-label="Drag to reorder"
      >
        ⠿
      </button>

      <button
        type="button"
        onClick={() => onSelect(item.id)}
        className={`flex flex-1 items-center gap-2 truncate text-left text-sm ${
          isSelected ? 'font-medium text-indigo-300' : 'text-slate-200'
        }`}
      >
        <span
          className={`h-1.5 w-1.5 shrink-0 rounded-full ${
            item.visible ? 'bg-emerald-400' : 'bg-slate-600'
          }`}
        />
        <span className="truncate">{item.name || item.localization}</span>
        {item.offerCount > 0 && (
          <span className="shrink-0 rounded-full bg-slate-800 px-1.5 py-0.5 text-[10px] text-slate-400">
            {item.offerCount}
          </span>
        )}
      </button>
    </div>
  )
}
