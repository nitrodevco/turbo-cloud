import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useRooms } from '../../api/rooms'

export function RoomsListPage() {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const { data, isLoading, isError } = useRooms(search, page)

  return (
    <div className="p-6">
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">Rooms</h1>
        <input
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
          placeholder="Search by room name…"
          className="w-64 rounded-md border border-slate-700 bg-slate-900 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
        />
      </div>

      {isLoading && <p className="text-sm text-slate-400">Loading…</p>}
      {isError && <p className="text-sm text-red-400">Failed to load rooms.</p>}

      {data && (
        <>
          <div className="overflow-hidden rounded-lg border border-slate-800">
            <table className="w-full text-left text-sm">
              <thead className="bg-slate-900 text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  <th className="px-4 py-2">Name</th>
                  <th className="px-4 py-2">Owner</th>
                  <th className="px-4 py-2">Users</th>
                  <th className="px-4 py-2">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {data.items.map((room) => (
                  <tr key={room.id} className="hover:bg-slate-900/60">
                    <td className="px-4 py-2">
                      <Link
                        to={`/rooms/${room.id}`}
                        className="font-medium text-indigo-300 hover:underline"
                      >
                        {room.name}
                      </Link>
                    </td>
                    <td className="px-4 py-2 text-slate-400">{room.ownerName}</td>
                    <td className="px-4 py-2 text-slate-400">
                      {room.usersNow} / {room.playersMax}
                    </td>
                    <td className="px-4 py-2">
                      <span
                        className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs ${
                          room.isActive
                            ? 'bg-emerald-500/15 text-emerald-300'
                            : 'bg-slate-700/40 text-slate-400'
                        }`}
                      >
                        <span
                          className={`h-1.5 w-1.5 rounded-full ${room.isActive ? 'bg-emerald-400' : 'bg-slate-500'}`}
                        />
                        {room.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                  </tr>
                ))}
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-4 py-6 text-center text-slate-500">
                      No rooms found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex items-center justify-between text-sm text-slate-400">
            <span>{data.totalCount} total</span>
            <div className="flex gap-2">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-md border border-slate-700 px-2 py-1 disabled:opacity-40"
              >
                Prev
              </button>
              <button
                type="button"
                disabled={page * data.pageSize >= data.totalCount}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-md border border-slate-700 px-2 py-1 disabled:opacity-40"
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
