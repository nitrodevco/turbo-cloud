import { useEffect, useState } from 'react'
import {
  CURRENCY_TYPES,
  useCreateOffer,
  useCurrencyTypes,
  useDeleteOffer,
  useOfferDetail,
  useUpdateOffer,
} from '../../../api/catalog'
import { Modal } from '../../../components/Modal'
import { ProductEditorModal } from './ProductEditorModal'

interface Props {
  pageId: number
  offerId: number | null
  onClose: () => void
}

export function OfferEditorModal({ pageId, offerId, onClose }: Props) {
  const { data: offer } = useOfferDetail(offerId)
  const { data: currencyTypes } = useCurrencyTypes()

  const [localizationId, setLocalizationId] = useState('')
  const [costCredits, setCostCredits] = useState(0)
  const [costCurrency, setCostCurrency] = useState(0)
  const [currencyTypeId, setCurrencyTypeId] = useState<number | null>(null)
  const [canGift, setCanGift] = useState(true)
  const [canBundle, setCanBundle] = useState(true)
  const [clubLevel, setClubLevel] = useState(0)
  const [visible, setVisible] = useState(true)
  const [editingProductId, setEditingProductId] = useState<number | 'new' | null>(null)

  useEffect(() => {
    if (!offer) return
    setLocalizationId(offer.localizationId)
    setCostCredits(offer.costCredits)
    setCostCurrency(offer.costCurrency)
    setCurrencyTypeId(offer.currencyTypeId)
    setCanGift(offer.canGift)
    setCanBundle(offer.canBundle)
    setClubLevel(offer.clubLevel)
    setVisible(offer.visible)
  }, [offer])

  const createOffer = useCreateOffer()
  const updateOffer = useUpdateOffer(offerId ?? 0)
  const deleteOffer = useDeleteOffer(offerId ?? 0, pageId)

  const isSaving = createOffer.isPending || updateOffer.isPending

  function handleSave() {
    const request = {
      pageId,
      localizationId,
      costCredits,
      costCurrency,
      currencyTypeId,
      canGift,
      canBundle,
      clubLevel,
      visible,
    }

    if (offerId !== null) {
      updateOffer.mutate(request)
    } else {
      createOffer.mutate(request, { onSuccess: onClose })
    }
  }

  function handleDelete() {
    deleteOffer.mutate(undefined, { onSuccess: onClose })
  }

  const editingProduct =
    editingProductId === 'new'
      ? null
      : (offer?.products.find((p) => p.id === editingProductId) ?? null)

  return (
    <>
      <Modal title={offerId !== null ? 'Edit offer' : 'New offer'} onClose={onClose} wide>
        <div className="flex flex-col gap-4">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Localization key
            </label>
            <input
              value={localizationId}
              onChange={(e) => setLocalizationId(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-400">
                Cost (credits)
              </label>
              <input
                type="number"
                min={0}
                value={costCredits}
                onChange={(e) => setCostCredits(Number(e.target.value))}
                className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-400">
                Cost (currency)
              </label>
              <input
                type="number"
                min={0}
                value={costCurrency}
                onChange={(e) => setCostCurrency(Number(e.target.value))}
                className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-400">
                Currency type
              </label>
              <select
                value={currencyTypeId ?? ''}
                onChange={(e) =>
                  setCurrencyTypeId(e.target.value === '' ? null : Number(e.target.value))
                }
                className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
              >
                <option value="">None</option>
                {(currencyTypes ?? CURRENCY_TYPES.map((c) => ({ id: c.value, name: c.label }))).map(
                  (c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ),
                )}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-400">
                Club level
              </label>
              <input
                type="number"
                min={0}
                value={clubLevel}
                onChange={(e) => setClubLevel(Number(e.target.value))}
                className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
              />
            </div>
            <div className="flex items-end gap-4 pb-1.5">
              <label className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={canGift}
                  onChange={(e) => setCanGift(e.target.checked)}
                  className="accent-indigo-500"
                />
                Can gift
              </label>
              <label className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={canBundle}
                  onChange={(e) => setCanBundle(e.target.checked)}
                  className="accent-indigo-500"
                />
                Can bundle
              </label>
              <label className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={visible}
                  onChange={(e) => setVisible(e.target.checked)}
                  className="accent-indigo-500"
                />
                Visible
              </label>
            </div>
          </div>

          {offerId !== null && (
            <div className="rounded-md border border-slate-800 bg-slate-950/50 p-3">
              <div className="mb-2 flex items-center justify-between">
                <h3 className="text-xs font-semibold text-slate-400">Products</h3>
                <button
                  type="button"
                  onClick={() => setEditingProductId('new')}
                  className="rounded-md border border-slate-700 px-2 py-1 text-xs text-slate-300 hover:bg-slate-800"
                >
                  + Add product
                </button>
              </div>

              {offer && offer.products.length > 0 ? (
                <ul className="divide-y divide-slate-800">
                  {offer.products.map((product) => (
                    <li
                      key={product.id}
                      className="flex items-center justify-between py-1.5 text-sm"
                    >
                      <button
                        type="button"
                        onClick={() => setEditingProductId(product.id)}
                        className="truncate text-left text-indigo-300 hover:underline"
                      >
                        {product.furnitureName ?? `Product #${product.id}`}
                      </button>
                      <span className="text-xs text-slate-500">×{product.quantity}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-xs text-slate-500">No products yet.</p>
              )}
            </div>
          )}

          <div className="flex items-center justify-between pt-2">
            {offerId !== null ? (
              <button
                type="button"
                onClick={handleDelete}
                disabled={deleteOffer.isPending}
                className="rounded-md bg-red-600/20 px-3 py-1.5 text-sm font-medium text-red-300 hover:bg-red-600/30 disabled:opacity-60"
              >
                Delete offer
              </button>
            ) : (
              <span />
            )}
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving || !localizationId.trim()}
              className="rounded-md bg-indigo-500 px-4 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-60"
            >
              Save
            </button>
          </div>
        </div>
      </Modal>

      {offerId !== null && editingProductId !== null && (
        <ProductEditorModal
          offerId={offerId}
          product={editingProduct}
          onClose={() => setEditingProductId(null)}
        />
      )}
    </>
  )
}
