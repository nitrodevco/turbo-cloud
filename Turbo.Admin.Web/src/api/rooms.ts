import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

export interface RoomListItem {
  id: number
  name: string
  ownerName: string
  usersNow: number
  playersMax: number
  isActive: boolean
}

export interface RoomListResponse {
  items: RoomListItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface RoomAvatarInfo {
  playerId: number
  name: string
  x: number
  y: number
}

export interface RoomLiveInfo {
  isActive: boolean
  population: number
  avatars: RoomAvatarInfo[]
}

export interface RoomDetail {
  id: number
  name: string
  description: string
  ownerName: string
  doorMode: number
  hasPassword: boolean
  playersMax: number
  whoCanMute: number
  whoCanKick: number
  whoCanBan: number
  live: RoomLiveInfo
}

export interface UpdateRoomRequest {
  name: string
  description: string
  doorMode: number
  password?: string
  playersMax: number
  whoCanMute: number
  whoCanKick: number
  whoCanBan: number
}

export function useRooms(search: string, page: number, pageSize = 25) {
  return useQuery({
    queryKey: ['rooms', search, page, pageSize],
    queryFn: () =>
      apiFetch<RoomListResponse>(
        `/api/admin/rooms/?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`,
      ),
    placeholderData: (prev) => prev,
    refetchInterval: 4000,
  })
}

export function useRoom(id: number | null) {
  return useQuery({
    queryKey: ['room', id],
    queryFn: () => apiFetch<RoomDetail>(`/api/admin/rooms/${id}`),
    enabled: id !== null,
    refetchInterval: 4000,
  })
}

export function useUpdateRoom(id: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpdateRoomRequest) =>
      apiFetch<void>(`/api/admin/rooms/${id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['room', id] })
      queryClient.invalidateQueries({ queryKey: ['rooms'] })
    },
  })
}

export function useKickFromRoom(roomId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (playerId: number) =>
      apiFetch<void>(`/api/admin/rooms/${roomId}/kick/${playerId}`, {
        method: 'POST',
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['room', roomId] }),
  })
}

export function useAlertRoom(roomId: number) {
  return useMutation({
    mutationFn: (message: string) =>
      apiFetch<void>(`/api/admin/rooms/${roomId}/alert`, {
        method: 'POST',
        body: JSON.stringify({ message }),
      }),
  })
}
