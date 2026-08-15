import { useState } from 'react'
import { useFurnitureSearch } from '../../../api/catalog'

interface Props {
  value: number | null
  onChange: (id: number | null, name: string | null) => void
}

export function FurniturePicker({ value, onChange }: Props) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const { data, isLoading } = useFurnitureSearch(query)

  return (
    <div className="relative">
      <input
        value={query}
        onChange={(e) => {
          setQuery(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        placeholder={value ? `Furniture #${value}` : 'Search furniture…'}
        className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
      />

      {open && (
        <div className="absolute z-10 mt-1 max-h-56 w-full overflow-y-auto rounded-md border border-slate-700 bg-slate-950 shadow-lg">
          {isLoading && <p className="px-3 py-2 text-xs text-slate-500">Searching…</p>}
          {!isLoading && data?.length === 0 && (
            <p className="px-3 py-2 text-xs text-slate-500">No matches.</p>
          )}
          {data?.map((item) => (
            <button
              key={item.id}
              type="button"
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => {
                onChange(item.id, item.name)
                setQuery(item.name)
                setOpen(false)
              }}
              className={`block w-full truncate px-3 py-1.5 text-left text-sm hover:bg-slate-800 ${
                item.id === value ? 'text-indigo-300' : 'text-slate-200'
              }`}
            >
              {item.name}{' '}
              <span className="text-xs text-slate-500">#{item.spriteId}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
