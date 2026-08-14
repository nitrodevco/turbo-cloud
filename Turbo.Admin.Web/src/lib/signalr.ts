import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'
import { useCallback, useEffect, useRef, useState } from 'react'
import { getStoredToken } from './auth'

const MAX_LINES = 2000

export function useConsoleHub() {
  const [lines, setLines] = useState<string[]>([])
  const [connected, setConnected] = useState(false)
  const connectionRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    const token = getStoredToken()

    if (!token) return

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/console', { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build()

    connection.on('history', (history: string[]) => setLines(history))
    connection.on('line', (line: string) =>
      setLines((prev) => [...prev, line].slice(-MAX_LINES)),
    )
    connection.onreconnected(() => setConnected(true))
    connection.onclose(() => setConnected(false))

    connection
      .start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false))

    connectionRef.current = connection

    return () => {
      void connection.stop()
    }
  }, [])

  const sendCommand = useCallback((input: string) => {
    void connectionRef.current?.invoke('SendCommandAsync', input)
  }, [])

  return { lines, connected, sendCommand }
}
