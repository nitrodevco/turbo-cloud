import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../lib/auth'

const NAV_ITEMS = [
  { to: '/users', label: 'Users' },
  { to: '/rooms', label: 'Rooms' },
  { to: '/catalog', label: 'Catalog' },
  { to: '/terminal', label: 'Terminal' },
]

export function Layout() {
  const { username, role, logout } = useAuth()

  return (
    <div className="flex h-screen bg-slate-950 text-slate-100">
      <aside className="flex w-56 shrink-0 flex-col border-r border-slate-800 bg-slate-900">
        <div className="px-4 py-5 text-lg font-semibold tracking-tight text-white">
          Turbo Admin
        </div>
        <nav className="flex flex-1 flex-col gap-1 px-2">
          {NAV_ITEMS.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-indigo-500/15 text-indigo-300'
                    : 'text-slate-400 hover:bg-slate-800 hover:text-slate-100'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="border-t border-slate-800 px-4 py-4 text-xs text-slate-500">
          <div className="text-slate-300">{username}</div>
          <div className="mb-2">{role}</div>
          <button
            type="button"
            onClick={logout}
            className="rounded-md border border-slate-700 px-2 py-1 text-slate-300 hover:bg-slate-800"
          >
            Sign out
          </button>
        </div>
      </aside>
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}
