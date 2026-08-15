import { useEffect, useState } from 'react'
import {
  useCreatePage,
  useDeletePage,
  usePageDetail,
  useUpdatePage,
} from '../../../api/catalog'

interface Props {
  pageId: number
  onSelectPage: (id: number) => void
  onManageOffer: (offerId: number | null) => void
}

export function PageDetailPanel({ pageId, onSelectPage, onManageOffer }: Props) {
  const { data: page, isLoading } = usePageDetail(pageId)
  const updatePage = useUpdatePage(pageId)
  const deletePage = useDeletePage(pageId)
  const createPage = useCreatePage()

  const [localization, setLocalization] = useState('')
  const [name, setName] = useState('')
  const [icon, setIcon] = useState(0)
  const [layout, setLayout] = useState('default_3x3')
  const [visible, setVisible] = useState(true)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  useEffect(() => {
    if (!page) return
    setLocalization(page.localization)
    setName(page.name ?? '')
    setIcon(page.icon)
    setLayout(page.layout)
    setVisible(page.visible)
    setDeleteError(null)
  }, [page])

  if (isLoading || !page) {
    return <div className="p-6 text-sm text-slate-400">Loading…</div>
  }

  function handleSave() {
    updatePage.mutate({
      parentId: page!.parentId,
      localization,
      name: name.trim() || null,
      icon,
      layout,
      imageData: page!.imageData,
      textData: page!.textData,
      visible,
    })
  }

  function handleDelete() {
    setDeleteError(null)
    deletePage.mutate(undefined, {
      onError: (err) => setDeleteError(err.message),
    })
  }

  function handleAddSubpage() {
    createPage.mutate(
      {
        parentId: pageId,
        localization: `${localization}_new`,
        name: 'New page',
        icon: 0,
        layout: 'default_3x3',
        imageData: null,
        textData: null,
        visible: true,
      },
      { onSuccess: (res) => onSelectPage(res.id) },
    )
  }

  return (
    <div className="p-6">
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-xl font-semibold text-white">{name || localization}</h1>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={handleAddSubpage}
            className="rounded-md border border-slate-700 px-3 py-1.5 text-sm text-slate-300 hover:bg-slate-800"
          >
            + Subpage
          </button>
          <button
            type="button"
            onClick={() => onManageOffer(null)}
            className="rounded-md border border-slate-700 px-3 py-1.5 text-sm text-slate-300 hover:bg-slate-800"
          >
            + Offer
          </button>
        </div>
      </div>

      <section className="mb-6 rounded-lg border border-slate-800 bg-slate-900 p-4">
        <h2 className="mb-4 text-sm font-semibold text-slate-300">Page settings</h2>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Localization key
            </label>
            <input
              value={localization}
              onChange={(e) => setLocalization(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">
              Display name
            </label>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">Icon</label>
            <input
              type="number"
              min={0}
              value={icon}
              onChange={(e) => setIcon(Number(e.target.value))}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-400">Layout</label>
            <input
              value={layout}
              onChange={(e) => setLayout(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-indigo-500"
            />
          </div>
          <div className="col-span-2">
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

        <div className="mt-4 flex items-center justify-between border-t border-slate-800 pt-4">
          <div>
            <button
              type="button"
              onClick={handleDelete}
              disabled={deletePage.isPending}
              className="rounded-md bg-red-600/20 px-3 py-1.5 text-sm font-medium text-red-300 hover:bg-red-600/30 disabled:opacity-60"
            >
              Delete page
            </button>
            {deleteError && <p className="mt-1 text-xs text-red-400">{deleteError}</p>}
          </div>
          <button
            type="button"
            onClick={handleSave}
            disabled={updatePage.isPending}
            className="rounded-md bg-indigo-500 px-4 py-1.5 text-sm font-medium text-white hover:bg-indigo-400 disabled:opacity-60"
          >
            Save
          </button>
        </div>
      </section>

      <section className="rounded-lg border border-slate-800 bg-slate-900 p-4">
        <h2 className="mb-3 text-sm font-semibold text-slate-300">
          Offers ({page.offers.length})
        </h2>

        {page.offers.length > 0 ? (
          <div className="overflow-hidden rounded-lg border border-slate-800">
            <table className="w-full text-left text-sm">
              <thead className="bg-slate-950 text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  <th className="px-3 py-2">Localization</th>
                  <th className="px-3 py-2">Cost</th>
                  <th className="px-3 py-2">Club</th>
                  <th className="px-3 py-2">Products</th>
                  <th className="px-3 py-2">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800">
                {page.offers.map((offer) => (
                  <tr key={offer.id} className="hover:bg-slate-900/60">
                    <td className="px-3 py-2">
                      <button
                        type="button"
                        onClick={() => onManageOffer(offer.id)}
                        className="font-medium text-indigo-300 hover:underline"
                      >
                        {offer.localizationId}
                      </button>
                    </td>
                    <td className="px-3 py-2 text-slate-400">
                      {offer.costCredits > 0 && `${offer.costCredits}c `}
                      {offer.costCurrency > 0 && `${offer.costCurrency}★`}
                      {offer.costCredits === 0 && offer.costCurrency === 0 && '—'}
                    </td>
                    <td className="px-3 py-2 text-slate-400">
                      {offer.clubLevel > 0 ? offer.clubLevel : '—'}
                    </td>
                    <td className="px-3 py-2 text-slate-400">{offer.productCount}</td>
                    <td className="px-3 py-2">
                      <span
                        className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs ${
                          offer.visible
                            ? 'bg-emerald-500/15 text-emerald-300'
                            : 'bg-slate-700/40 text-slate-400'
                        }`}
                      >
                        <span
                          className={`h-1.5 w-1.5 rounded-full ${offer.visible ? 'bg-emerald-400' : 'bg-slate-500'}`}
                        />
                        {offer.visible ? 'Visible' : 'Hidden'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-sm text-slate-400">No offers on this page yet.</p>
        )}
      </section>
    </div>
  )
}
