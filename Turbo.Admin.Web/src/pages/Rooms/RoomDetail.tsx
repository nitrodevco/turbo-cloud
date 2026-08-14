import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  useAlertRoom,
  useKickFromRoom,
  useRoom,
  useUpdateRoom,
} from '../../api/rooms'

const DOOR_MODES = ['Open', 'Locked', 'Password', 'Invisible']
const MOD_SETTINGS = ['Owner only', 'Rights holders', 'Group', 'Rights or group']

export function RoomDetailPage() {
  const params = useParams()
  const id = Number(params.id)
  const { data: room, isLoading } = useRoom(Number.isFinite(id) ? id : null)
  const updateRoom = useUpdateRoom(id)
  const kickPlayer = useKickFromRoom(id)
  const alertRoom = useAlertRoom(id)

  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [doorMode, setDoorMode] = useState(0)
  const [playersMax, setPlayersMax] = useState(25)
  const [whoCanMute, setWhoCanMute] = useState(0)
  const [whoCanKick, setWhoCanKick] = useState(0)
  const [whoCanBan, setWhoCanBan] = useState(0)
  const [alertMessage, setAlertMessage] = useState('')

  useEffect(() => {
    if (!room) return
    setName(room.name)
    setDescription(room.description)
    setDoorMode(room.doorMode)
    setPlayersMax(room.playersMax)
    setWhoCanMute(room.whoCanMute)
    setWhoCanKick(room.whoCanKick)
    setWhoCanBan(room.whoCanBan)
  }, [room])

  if (isLoading || !room) {
    return <div className="p-6 text-sm text-slate-400">Loading…</div>
  }

  function handleSave() {
    updateRoom.mutate({
      name,
      description,
      doorMode,
      playersMax,
      whoCanMute,
      whoCanKick,
      whoCanBan,
    })
  }

  function handleAlert() {
    if (!alertMessage.trim()) return
    alertRoom.mutate(alertMessage, { onSuccess: () => setAlertMessage('') })
  }

  return (
    <div className="mx-auto max-w-3xl p-6">
      <Link to="/rooms" className="text-sm text-slate-400 hover:text-slate-200">
        ← Back to rooms
      </Link>

      <div className="mt-3 mb-6 flex items-center gap-3">
        <h1 className="text-xl font-semibold text-white">{room.name}</h1>
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs ${
            room.live.isActive
              ? 'bg-emerald-500/15 text-emerald-300'
              : 'bg-slate-700/40 text-slate-400'
          }`}
        >
          <span
            className={`h-1.5 w-1.5 rounded-full ${room.live.isActive ? 'bg-emerald-400' : 'bg-slate-500'}`}
          />
          {room.live.isActive ? 'Active' : 'Inactive'}
        </span>
        <span className="text-sm text-slate-500">Owner: {room.ownerName}</span>
      </div>

      {room.live.isActive && (
        <section className="mb-6 rounded-lg border border-emerald-900/60 bg-emerald-950/20 p-4">
          <h2 className="mb-3 text-sm font-semibold text-emerald-300">
            Live — {room.live.population} in room
          </h2>

          <div className="mb-3 flex flex-wrap items-center gap-2">
            <input
              value={alertMessage}
              onChange={(e) => setAlertMessage(e.target.value)}
              placeholder="Message to everyone in the room…"
              className="w-72 rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
            <button
              type="button"
              onClick={handleAlert}
              disabled={alertRoom.isPending || room.live.population === 0}
              className="rounded-md bg-indigo-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-60"
            >
              Alert room
            </button>
          </div>

          {room.live.avatars.length > 0 ? (
            <ul className="divide-y divide-emerald-900/40">
              {room.live.avatars.map((avatar) => (
                <li
                  key={avatar.playerId}
                  className="flex items-center justify-between py-1.5 text-sm"
                >
                  <Link
                    to={`/users/${avatar.playerId}`}
                    className="text-indigo-300 hover:underline"
                  >
                    {avatar.name}
                  </Link>
                  <button
                    type="button"
                    onClick={() => kickPlayer.mutate(avatar.playerId)}
                    disabled={kickPlayer.isPending}
                    className="rounded-md bg-red-600 px-2 py-1 text-xs font-medium text-white hover:bg-red-500 disabled:opacity-60"
                  >
                    Kick
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-slate-400">No one is currently in this room.</p>
          )}
        </section>
      )}

      <section className="rounded-lg border border-slate-800 bg-slate-900 p-4">
        <h2 className="mb-4 text-sm font-semibold text-slate-300">Settings</h2>

        <div className="grid grid-cols-2 gap-4">
          <div className="col-span-2">
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Name
            </label>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div className="col-span-2">
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Description
            </label>
            <input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Door mode
            </label>
            <select
              value={doorMode}
              onChange={(e) => setDoorMode(Number(e.target.value))}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            >
              {DOOR_MODES.map((label, value) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Max users
            </label>
            <input
              type="number"
              value={playersMax}
              onChange={(e) => setPlayersMax(Number(e.target.value))}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>

          {(
            [
              ['Who can mute', whoCanMute, setWhoCanMute],
              ['Who can kick', whoCanKick, setWhoCanKick],
              ['Who can ban', whoCanBan, setWhoCanBan],
            ] as const
          ).map(([label, value, setValue]) => (
            <div key={label}>
              <label className="mb-1 block text-xs font-medium text-slate-400">
                {label}
              </label>
              <select
                value={value}
                onChange={(e) => setValue(Number(e.target.value))}
                className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
              >
                <option value={0}>{MOD_SETTINGS[0]}</option>
                <option value={1}>{MOD_SETTINGS[1]}</option>
                <option value={4}>{MOD_SETTINGS[2]}</option>
                <option value={5}>{MOD_SETTINGS[3]}</option>
              </select>
            </div>
          ))}
        </div>

        <div className="mt-4 flex items-center gap-3">
          <button
            type="button"
            onClick={handleSave}
            disabled={updateRoom.isPending}
            className="rounded-md bg-indigo-500 px-4 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-60"
          >
            Save changes
          </button>
          {updateRoom.isSuccess && (
            <span className="text-sm text-emerald-400">Saved.</span>
          )}
        </div>
      </section>
    </div>
  )
}
