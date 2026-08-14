import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  useAlertUser,
  useKickUser,
  useUpdateUser,
  useUser,
} from '../../api/users'

export function UserDetailPage() {
  const params = useParams()
  const id = Number(params.id)
  const { data: user, isLoading } = useUser(Number.isFinite(id) ? id : null)
  const updateUser = useUpdateUser(id)
  const kickUser = useKickUser(id)
  const alertUser = useAlertUser(id)

  const [name, setName] = useState('')
  const [motto, setMotto] = useState('')
  const [figure, setFigure] = useState('')
  const [gender, setGender] = useState(0)
  const [alertMessage, setAlertMessage] = useState('')

  useEffect(() => {
    if (!user) return
    setName(user.name)
    setMotto(user.motto)
    setFigure(user.figure)
    setGender(user.gender)
  }, [user])

  if (isLoading || !user) {
    return <div className="p-6 text-sm text-slate-400">Loading…</div>
  }

  function handleSave() {
    updateUser.mutate({ name, motto, figure, gender })
  }

  function handleAlert() {
    if (!alertMessage.trim()) return
    alertUser.mutate(alertMessage, { onSuccess: () => setAlertMessage('') })
  }

  return (
    <div className="mx-auto max-w-3xl p-6">
      <Link to="/users" className="text-sm text-slate-400 hover:text-slate-200">
        ← Back to users
      </Link>

      <div className="mt-3 mb-6 flex items-center gap-3">
        <h1 className="text-xl font-semibold text-white">{user.name}</h1>
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs ${
            user.live.isOnline
              ? 'bg-emerald-500/15 text-emerald-300'
              : 'bg-slate-700/40 text-slate-400'
          }`}
        >
          <span
            className={`h-1.5 w-1.5 rounded-full ${user.live.isOnline ? 'bg-emerald-400' : 'bg-slate-500'}`}
          />
          {user.live.isOnline ? 'Online' : 'Offline'}
        </span>
      </div>

      {user.live.isOnline && (
        <section className="mb-6 rounded-lg border border-emerald-900/60 bg-emerald-950/20 p-4">
          <h2 className="mb-3 text-sm font-semibold text-emerald-300">Live</h2>
          <div className="mb-3 text-sm text-slate-300">
            {user.live.currentRoomId ? (
              <>
                Currently in{' '}
                <Link
                  to={`/rooms/${user.live.currentRoomId}`}
                  className="text-indigo-300 hover:underline"
                >
                  room #{user.live.currentRoomId}
                </Link>
              </>
            ) : (
              'Online, not in a room'
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <input
              value={alertMessage}
              onChange={(e) => setAlertMessage(e.target.value)}
              placeholder="Message to send…"
              className="w-64 rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
            <button
              type="button"
              onClick={handleAlert}
              disabled={alertUser.isPending}
              className="rounded-md bg-indigo-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-60"
            >
              Send alert
            </button>
            {user.live.currentRoomId && (
              <button
                type="button"
                onClick={() => kickUser.mutate()}
                disabled={kickUser.isPending}
                className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-500 disabled:opacity-60"
              >
                Kick from room
              </button>
            )}
          </div>
        </section>
      )}

      <section className="rounded-lg border border-slate-800 bg-slate-900 p-4">
        <h2 className="mb-4 text-sm font-semibold text-slate-300">Profile</h2>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Name
            </label>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Gender
            </label>
            <select
              value={gender}
              onChange={(e) => setGender(Number(e.target.value))}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            >
              <option value={0}>Male</option>
              <option value={1}>Female</option>
            </select>
          </div>
          <div className="col-span-2">
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Motto
            </label>
            <input
              value={motto}
              onChange={(e) => setMotto(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div className="col-span-2">
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Figure
            </label>
            <input
              value={figure}
              onChange={(e) => setFigure(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
        </div>

        <div className="mt-4 flex items-center gap-3">
          <button
            type="button"
            onClick={handleSave}
            disabled={updateUser.isPending}
            className="rounded-md bg-indigo-500 px-4 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-60"
          >
            Save changes
          </button>
          {updateUser.isSuccess && (
            <span className="text-sm text-emerald-400">Saved.</span>
          )}
        </div>
      </section>
    </div>
  )
}
