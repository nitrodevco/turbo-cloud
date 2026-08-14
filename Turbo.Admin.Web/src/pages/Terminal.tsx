import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { useConsoleHub } from '../lib/signalr'

export function TerminalPage() {
  const { lines, connected, sendCommand } = useConsoleHub()
  const [input, setInput] = useState('')
  const scrollRef = useRef<HTMLDivElement>(null)

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
      <div className="mb-3 flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">Terminal</h1>
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
          {connected ? 'Connected' : 'Disconnected'}
        </span>
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
