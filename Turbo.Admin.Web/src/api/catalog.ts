import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from './client'

export const PRODUCT_TYPES = [
  { value: 0, label: 'Floor' },
  { value: 1, label: 'Wall' },
  { value: 2, label: 'Effect' },
  { value: 3, label: 'Badge' },
  { value: 4, label: 'Robot' },
  { value: 5, label: 'Habbo Club' },
  { value: 6, label: 'Pet' },
] as const

export const CURRENCY_TYPES = [
  { value: 1, label: 'Credits' },
  { value: 2, label: 'Silver' },
  { value: 3, label: 'Emeralds' },
  { value: 4, label: 'Activity Points' },
] as const

export interface CatalogPageTreeItem {
  id: number
  parentId: number | null
  localization: string
  name: string | null
  icon: number
  layout: string
  sortOrder: number
  visible: boolean
  offerCount: number
  children: CatalogPageTreeItem[]
}

export interface CatalogOfferSummary {
  id: number
  localizationId: string
  costCredits: number
  costCurrency: number
  currencyTypeId: number | null
  clubLevel: number
  visible: boolean
  productCount: number
}

export interface CatalogPageDetail {
  id: number
  parentId: number | null
  localization: string
  name: string | null
  icon: number
  layout: string
  imageData: string[]
  textData: string[]
  visible: boolean
  offers: CatalogOfferSummary[]
}

export interface CatalogProductItem {
  id: number
  productType: number
  furnitureDefinitionId: number | null
  furnitureName: string | null
  furnitureSpriteId: number | null
  extraParam: string | null
  quantity: number
}

export interface CatalogOfferDetail {
  id: number
  pageId: number
  localizationId: string
  costCredits: number
  costCurrency: number
  currencyTypeId: number | null
  canGift: boolean
  canBundle: boolean
  clubLevel: number
  visible: boolean
  products: CatalogProductItem[]
}

export interface UpsertPageRequest {
  parentId: number | null
  localization: string
  name: string | null
  icon: number
  layout: string
  imageData: string[] | null
  textData: string[] | null
  visible: boolean
}

export interface PageOrderEntry {
  pageId: number
  parentId: number | null
  sortOrder: number
}

export interface UpsertOfferRequest {
  pageId: number
  localizationId: string
  costCredits: number
  costCurrency: number
  currencyTypeId: number | null
  canGift: boolean
  canBundle: boolean
  clubLevel: number
  visible: boolean
}

export interface UpsertProductRequest {
  offerId: number
  productType: number
  furnitureDefinitionId: number | null
  extraParam: string | null
  quantity: number
}

export interface FurnitureDefinitionListItem {
  id: number
  name: string
  spriteId: number
  productType: number
}

export interface CurrencyTypeListItem {
  id: number
  name: string
  currencyType: number
}

const TREE_KEY = ['catalog', 'pages']

export function useCatalogTree() {
  return useQuery({
    queryKey: TREE_KEY,
    queryFn: () => apiFetch<CatalogPageTreeItem[]>('/api/admin/catalog/pages'),
  })
}

export function usePageDetail(id: number | null) {
  return useQuery({
    queryKey: ['catalog', 'page', id],
    queryFn: () => apiFetch<CatalogPageDetail>(`/api/admin/catalog/pages/${id}`),
    enabled: id !== null,
  })
}

export function useCreatePage() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpsertPageRequest) =>
      apiFetch<{ id: number }>('/api/admin/catalog/pages', {
        method: 'POST',
        body: JSON.stringify(request),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: TREE_KEY }),
  })
}

export function useUpdatePage(id: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpsertPageRequest) =>
      apiFetch<void>(`/api/admin/catalog/pages/${id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TREE_KEY })
      queryClient.invalidateQueries({ queryKey: ['catalog', 'page', id] })
    },
  })
}

export function useDeletePage(id: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => apiFetch<void>(`/api/admin/catalog/pages/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: TREE_KEY }),
  })
}

export function useReorderPages() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (entries: PageOrderEntry[]) =>
      apiFetch<void>('/api/admin/catalog/pages/reorder', {
        method: 'POST',
        body: JSON.stringify({ entries }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: TREE_KEY }),
  })
}

export function useOfferDetail(id: number | null) {
  return useQuery({
    queryKey: ['catalog', 'offer', id],
    queryFn: () => apiFetch<CatalogOfferDetail>(`/api/admin/catalog/offers/${id}`),
    enabled: id !== null,
  })
}

export function useCreateOffer() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpsertOfferRequest) =>
      apiFetch<{ id: number }>('/api/admin/catalog/offers', {
        method: 'POST',
        body: JSON.stringify(request),
      }),
    onSuccess: (_data, request) => {
      queryClient.invalidateQueries({ queryKey: ['catalog', 'page', request.pageId] })
      queryClient.invalidateQueries({ queryKey: TREE_KEY })
    },
  })
}

export function useUpdateOffer(id: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpsertOfferRequest) =>
      apiFetch<void>(`/api/admin/catalog/offers/${id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      }),
    onSuccess: (_data, request) => {
      queryClient.invalidateQueries({ queryKey: ['catalog', 'offer', id] })
      queryClient.invalidateQueries({ queryKey: ['catalog', 'page', request.pageId] })
      queryClient.invalidateQueries({ queryKey: TREE_KEY })
    },
  })
}

export function useDeleteOffer(id: number, pageId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => apiFetch<void>(`/api/admin/catalog/offers/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['catalog', 'page', pageId] })
      queryClient.invalidateQueries({ queryKey: TREE_KEY })
    },
  })
}

export function useCreateProduct() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpsertProductRequest) =>
      apiFetch<{ id: number }>('/api/admin/catalog/products', {
        method: 'POST',
        body: JSON.stringify(request),
      }),
    onSuccess: (_data, request) =>
      queryClient.invalidateQueries({ queryKey: ['catalog', 'offer', request.offerId] }),
  })
}

export function useUpdateProduct(id: number, offerId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpsertProductRequest) =>
      apiFetch<void>(`/api/admin/catalog/products/${id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['catalog', 'offer', offerId] }),
  })
}

export function useDeleteProduct(id: number, offerId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => apiFetch<void>(`/api/admin/catalog/products/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['catalog', 'offer', offerId] }),
  })
}

export function useFurnitureSearch(search: string) {
  return useQuery({
    queryKey: ['catalog', 'furniture', search],
    queryFn: () =>
      apiFetch<FurnitureDefinitionListItem[]>(
        `/api/admin/catalog/furniture?search=${encodeURIComponent(search)}`,
      ),
    placeholderData: (prev) => prev,
  })
}

export function useCurrencyTypes() {
  return useQuery({
    queryKey: ['catalog', 'currency-types'],
    queryFn: () => apiFetch<CurrencyTypeListItem[]>('/api/admin/catalog/currency-types'),
  })
}
