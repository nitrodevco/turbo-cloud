import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

export interface UserListItem {
  id: number
  name: string
  motto: string
  figure: string
  isOnline: boolean
  createdAt: string
}

export interface UserListResponse {
  items: UserListItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface UserLiveInfo {
  isOnline: boolean
  currentRoomId: number | null
  activeSinceUtc: string | null
}

export interface UserDetail {
  id: number
  name: string
  motto: string
  figure: string
  gender: number
  playerPerks: number
  createdAt: string
  live: UserLiveInfo
}

export interface UpdateUserRequest {
  name?: string
  motto?: string
  figure?: string
  gender?: number
  playerPerks?: number
}

export function useUsers(search: string, page: number, pageSize = 25) {
  return useQuery({
    queryKey: ['users', search, page, pageSize],
    queryFn: () =>
      apiFetch<UserListResponse>(
        `/api/admin/users/?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`,
      ),
    placeholderData: (prev) => prev,
  })
}

export function useUser(id: number | null) {
  return useQuery({
    queryKey: ['user', id],
    queryFn: () => apiFetch<UserDetail>(`/api/admin/users/${id}`),
    enabled: id !== null,
    refetchInterval: 4000,
  })
}

export function useUpdateUser(id: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpdateUserRequest) =>
      apiFetch<void>(`/api/admin/users/${id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['user', id] })
      queryClient.invalidateQueries({ queryKey: ['users'] })
    },
  })
}

export function useKickUser(id: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => apiFetch<void>(`/api/admin/users/${id}/kick`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['user', id] }),
  })
}

export function useAlertUser(id: number) {
  return useMutation({
    mutationFn: (message: string) =>
      apiFetch<void>(`/api/admin/users/${id}/alert`, {
        method: 'POST',
        body: JSON.stringify({ message }),
      }),
  })
}
