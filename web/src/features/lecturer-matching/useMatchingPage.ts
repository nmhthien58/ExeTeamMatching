import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { UseMutationResult } from '@tanstack/react-query'
import { toApiError } from '@/shared/api/apiClient'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import type { AdvisorMatchingRun } from './api'

export type MatchingMessage = { title: string; detail?: string }

/** Lần xem trước đã được chốt / bỏ ở nơi khác: đóng dialog, báo trên trang. */
const isStale = (error: unknown) => toApiError(error).code === 'run-not-preview'

/** Lọc, tìm, trang của W19, W28 trên URL. */
export function useMatchingParams(filterParam: string) {
  const [params, setParams] = useSearchParams()

  const update = (patch: Record<string, string | undefined>, resetPage = true) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) {
      if (!v) next.delete(k)
      else next.set(k, v)
    }
    if (resetPage) next.delete('page')
    setParams(next, { replace: true })
  }

  return {
    filterParam,
    withoutAdvisor: params.get(filterParam) === '1',
    search: params.get('q') ?? '',
    page: Math.max(1, Number(params.get('page')) || 1),
    update,
  }
}

/** Luồng "Ghép sớm" của W19, W28: xem trước → chốt / bỏ, lỗi hiện trên trang hoặc trong dialog. */
export function useEarlyMatching({
  preview,
  confirm,
  discard,
  errorMessage,
  confirmedMessage,
}: {
  preview: UseMutationResult<AdvisorMatchingRun, Error, void>
  confirm: UseMutationResult<AdvisorMatchingRun, Error, string>
  discard: UseMutationResult<void, Error, string>
  errorMessage: (error: unknown) => MatchingMessage
  confirmedMessage: (run: AdvisorMatchingRun) => string
}) {
  const snackbar = useSnackbar()
  const [previewRun, setPreviewRun] = useState<AdvisorMatchingRun | null>(null)
  const [pageError, setPageError] = useState<MatchingMessage | null>(null)

  const closePreview = () => {
    setPreviewRun(null)
    confirm.reset()
    discard.reset()
  }

  const onStaleError = (error: unknown) => {
    if (isStale(error)) {
      closePreview()
      setPageError(errorMessage(error))
    }
  }

  return {
    snackbar,
    previewRun,
    setPreviewRun,
    pageError,
    setPageError,
    closePreview,
    previewing: preview.isPending,
    confirming: confirm.isPending,
    discarding: discard.isPending,
    dialogError: confirm.error ? errorMessage(confirm.error) : discard.error ? errorMessage(discard.error) : null,
    startPreview: () => {
      setPageError(null)
      preview.mutate(undefined, {
        onSuccess: (run) => setPreviewRun(run),
        onError: (error) => setPageError(errorMessage(error)),
      })
    },
    onConfirm: () => {
      if (!previewRun) return
      confirm.mutate(previewRun.id, {
        onSuccess: (run) => {
          closePreview()
          snackbar.show(confirmedMessage(run))
        },
        onError: onStaleError,
      })
    },
    onDiscard: () => {
      if (!previewRun) return
      discard.mutate(previewRun.id, {
        onSuccess: () => {
          closePreview()
          snackbar.show('Đã bỏ kết quả xem trước')
        },
        onError: onStaleError,
      })
    },
  }
}

export type EarlyMatching = ReturnType<typeof useEarlyMatching>
export type MatchingParams = ReturnType<typeof useMatchingParams>
