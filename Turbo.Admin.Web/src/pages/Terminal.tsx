import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import {
  useEmulatorStatus,
  useRestartEmulator,
  useStartEmulator,
  useStopEmulator,
} from '../api/emulator'
import { useConsoleHub } from '../lib/signalr'

const STATUS_STYLES: Record<string, string> = {
  Running: 'bg-emerald-500/15 text-emerald-300',
  Starting: 'bg-amber-500/15 text-amber-300',
  Stopping: 'bg-amber-500/15 text-amber-300',
  Stopped: 'bg-slate-700/40 text-slate-400',
}

const STATUS_DOT: Record<string, string> = {
  Running: 'bg-emerald-400',
  Starting: 'bg-amber-400',
  Stopping: 'bg-amber-400',
  Stopped: 'bg-slate-500',
}

export function TerminalPage() {
  const { lines, connected, sendCommand } = useConsoleHub()
  const [input, setInput] = useState('')
  const scrollRef = useRef<HTMLDivElement>(null)

  const { data: emulatorStatus } = useEmulatorStatus()
  const startEmulator = useStartEmulator()
  const stopEmulator = useStopEmulator()
  const restartEmulator = useRestartEmulator()

  const status = emulatorStatus?.status ?? 'Stopped'
  const isBusy =
    startEmulator.isPending || stopEmulator.isPending || restartEmulator.isPending

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight })
  }, [lines])

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter' || !input.trim()) return

    sendCommand(input)
    setInput('')
  }

  return (
    <div className="flex h-full flex-col p-6">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold text-white">Terminal</h1>
          <span
            className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs ${STATUS_STYLES[status]}`}
          >
            <span className={`h-1.5 w-1.5 rounded-full ${STATUS_DOT[status]}`} />
            {status}
          </span>
          <span
            className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs ${
              connected
                ? 'bg-emerald-500/15 text-emerald-300'
                : 'bg-red-500/15 text-red-300'
            }`}
          >
            <span
              className={`h-1.5 w-1.5 rounded-full ${connected ? 'bg-emerald-400' : 'bg-red-400'}`}
            />
            {connected ? 'Panel connected' : 'Panel disconnected'}
          </span>
        </div>

        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => startEmulator.mutate()}
            disabled={isBusy || status === 'Running' || status === 'Starting'}
            className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-40"
          >
            Start
          </button>
          <button
            type="button"
            onClick={() => stopEmulator.mutate()}
            disabled={isBusy || status === 'Stopped' || status === 'Stopping'}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-500 disabled:opacity-40"
          >
            Stop
          </button>
          <button
            type="button"
            onClick={() => restartEmulator.mutate()}
            disabled={isBusy || status === 'Stopped'}
            className="rounded-md bg-indigo-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-40"
          >
            Restart
          </button>
        </div>
      </div>

      <div
        ref={scrollRef}
        className="flex-1 overflow-auto rounded-lg border border-slate-800 bg-black p-3 font-mono text-xs text-slate-200"
      >
        {lines.map((line, i) => (
          <div key={i} className="whitespace-pre-wrap break-all">
            {line}
          </div>
        ))}
      </div>

      <input
        value={input}
        onChange={(e) => setInput(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Type a command and press Enter…"
        className="mt-3 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 font-mono text-sm text-slate-100 outline-none focus:border-indigo-500"
      />
    </div>
  )
}
