import { useState } from 'react'
import { useCatalogTree, useCreatePage } from '../../api/catalog'
import { PageTree } from './components/PageTree'
import { PageDetailPanel } from './components/PageDetailPanel'
import { OfferEditorModal } from './components/OfferEditorModal'

export function CatalogEditorPage() {
  const { data: tree } = useCatalogTree()
  const createPage = useCreatePage()

  const [selectedPageId, setSelectedPageId] = useState<number | null>(null)
  const [offerModal, setOfferModal] = useState<{ pageId: number; offerId: number | null } | null>(
    null,
  )

  function handleCreateRootPage() {
    createPage.mutate(
      {
        parentId: null,
        localization: 'new_page',
        name: 'New page',
        icon: 0,
        layout: 'default_3x3',
        imageData: null,
        textData: null,
        visible: true,
      },
      { onSuccess: (res) => setSelectedPageId(res.id) },
    )
  }

  return (
    <div className="flex h-full">
      <aside className="w-72 shrink-0 overflow-y-auto border-r border-slate-800">
        <div className="flex items-center justify-between border-b border-slate-800 px-4 py-3">
          <h1 className="text-sm font-semibold text-white">Catalog</h1>
          <button
            type="button"
            onClick={handleCreateRootPage}
            className="rounded-md border border-slate-700 px-2 py-1 text-xs text-slate-300 hover:bg-slate-800"
          >
            + Page
          </button>
        </div>
        <PageTree selectedId={selectedPageId} onSelect={setSelectedPageId} />
      </aside>

      <main className="flex-1 overflow-y-auto">
        {selectedPageId !== null ? (
          <PageDetailPanel
            pageId={selectedPageId}
            onSelectPage={setSelectedPageId}
            onManageOffer={(offerId) => setOfferModal({ pageId: selectedPageId, offerId })}
          />
        ) : (
          <div className="flex h-full items-center justify-center text-sm text-slate-500">
            {tree && tree.length > 0
              ? 'Select a page to edit it.'
              : 'No catalog pages yet — create one to get started.'}
          </div>
        )}
      </main>

      {offerModal && (
        <OfferEditorModal
          pageId={offerModal.pageId}
          offerId={offerModal.offerId}
          onClose={() => setOfferModal(null)}
        />
      )}
    </div>
  )
}
