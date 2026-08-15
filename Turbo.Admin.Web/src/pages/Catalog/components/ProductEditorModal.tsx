import { useState } from 'react'
import {
  PRODUCT_TYPES,
  useCreateProduct,
  useDeleteProduct,
  useUpdateProduct,
  type CatalogProductItem,
} from '../../../api/catalog'
import { Modal } from '../../../components/Modal'
import { FurniturePicker } from './FurniturePicker'

interface Props {
  offerId: number
  product: CatalogProductItem | null
  onClose: () => void
}

export function ProductEditorModal({ offerId, product, onClose }: Props) {
  const [productType, setProductType] = useState(product?.productType ?? 0)
  const [furnitureId, setFurnitureId] = useState<number | null>(
    product?.furnitureDefinitionId ?? null,
  )
  const [furnitureName, setFurnitureName] = useState<string | null>(
    product?.furnitureName ?? null,
  )
  const [extraParam, setExtraParam] = useState(product?.extraParam ?? '')
  const [quantity, setQuantity] = useState(product?.quantity ?? 1)

  const createProduct = useCreateProduct()
  const updateProduct = useUpdateProduct(product?.id ?? 0, offerId)
  const deleteProduct = useDeleteProduct(product?.id ?? 0, offerId)

  const isSaving = createProduct.isPending || updateProduct.isPending

  function handleSave() {
    const request = {
      offerId,
      productType,
      furnitureDefinitionId: furnitureId,
      extraParam: extraParam.trim() || null,
      quantity,
    }

    if (product) {
      updateProduct.mutate(request, { onSuccess: onClose })
    } else {
      createProduct.mutate(request, { onSuccess: onClose })
    }
  }

  function handleDelete() {
    deleteProduct.mutate(undefined, { onSuccess: onClose })
  }

  return (
    <Modal title={product ? 'Edit product' : 'New product'} onClose={onClose}>
      <div className="flex flex-col gap-4">
        <div>
          <label className="mb-1 block text-xs font-medium text-slate-400">Product type</label>
          <select
            value={productType}
            onChange={(e) => setProductType(Number(e.target.value))}
            className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
          >
            {PRODUCT_TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="mb-1 block text-xs font-medium text-slate-400">
            Furniture definition
          </label>
          <FurniturePicker
            value={furnitureId}
            onChange={(id, name) => {
              setFurnitureId(id)
              setFurnitureName(name)
            }}
          />
          {furnitureName && (
            <p className="mt-1 text-xs text-slate-500">Selected: {furnitureName}</p>
          )}
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Extra param
            </label>
            <input
              value={extraParam}
              onChange={(e) => setExtraParam(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">Quantity</label>
            <input
              type="number"
              min={1}
              value={quantity}
              onChange={(e) => setQuantity(Number(e.target.value))}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
        </div>

        <div className="flex items-center justify-between pt-2">
          {product ? (
            <button
              type="button"
              onClick={handleDelete}
              disabled={deleteProduct.isPending}
              className="rounded-md bg-red-600/20 px-3 py-1.5 text-sm font-medium text-red-300 hover:bg-red-600/30 disabled:opacity-60"
            >
              Delete
            </button>
          ) : (
            <span />
          )}
          <button
            type="button"
            onClick={handleSave}
            disabled={isSaving}
            className="rounded-md bg-indigo-500 px-4 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-60"
          >
            Save
          </button>
        </div>
      </div>
    </Modal>
  )
}
