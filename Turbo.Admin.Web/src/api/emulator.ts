import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

export type EmulatorStatusValue = 'Stopped' | 'Starting' | 'Running' | 'Stopping'

export interface EmulatorStatusResponse {
  status: EmulatorStatusValue
}

export function useEmulatorStatus() {
  return useQuery({
    queryKey: ['emulator', 'status'],
    queryFn: () => apiFetch<EmulatorStatusResponse>('/api/admin/emulator/status'),
    refetchInterval: 2000,
  })
}

function useEmulatorAction(action: 'start' | 'stop' | 'restart') {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () =>
      apiFetch<EmulatorStatusResponse>(`/api/admin/emulator/${action}`, {
        method: 'POST',
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(['emulator', 'status'], data)
      queryClient.invalidateQueries({ queryKey: ['emulator', 'status'] })
    },
  })
}

export const useStartEmulator = () => useEmulatorAction('start')
export const useStopEmulator = () => useEmulatorAction('stop')
export const useRestartEmulator = () => useEmulatorAction('restart')
