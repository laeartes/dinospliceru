import { useState, useCallback, useRef, useEffect } from 'react'
import {
  startCompression as startCompressionApi,
  getCompressionJob,
  type CompressionRequest,
  type CompressionJobResponse,
} from '../api/compressionApi'
import { ClientErrorCodes, toErrorPayload } from '../api/apiError'

export type CompressionJobState =
  | { status: 'idle' }
  | { status: 'triggering'; request: CompressionRequest }
  | { status: 'polling'; job: CompressionJobResponse }
  | { status: 'ready'; job: CompressionJobResponse }
  | { status: 'failed'; job: CompressionJobResponse; error: string; code: string }
  | { status: 'error'; error: string; code: string }

export interface UseCompressionJobResult {
  compressionState: CompressionJobState
  startCompression: (request: CompressionRequest) => Promise<void>
  reset: () => void
  retry: () => void
}

const POLL_INTERVAL_MS = 1000
const MAX_POLL_RETRIES = 3
const POLL_TIMEOUT_MS = 600000

/** Manages triggering a compression job and polling its live progress until Ready or Failed. */
export function useCompressionJob(videoId: string | null): UseCompressionJobResult {
  const [compressionState, setCompressionState] = useState<CompressionJobState>({ status: 'idle' })
  const abortControllerRef = useRef<AbortController | null>(null)
  const pollTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const activePollCleanupRef = useRef<(() => void) | null>(null)
  const lastRequestRef = useRef<CompressionRequest | null>(null)

  const clearTimersAndControllers = useCallback(() => {
    if (activePollCleanupRef.current) {
      activePollCleanupRef.current()
      activePollCleanupRef.current = null
    }
    if (pollTimerRef.current) {
      clearTimeout(pollTimerRef.current)
      pollTimerRef.current = null
    }
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
      abortControllerRef.current = null
    }
  }, [])

  const reset = useCallback(() => {
    clearTimersAndControllers()
    lastRequestRef.current = null
    setCompressionState({ status: 'idle' })
  }, [clearTimersAndControllers])

  const currentVideoIdRef = useRef(videoId)
  useEffect(() => {
    if (currentVideoIdRef.current !== videoId) {
      currentVideoIdRef.current = videoId
      reset()
    }
  }, [videoId, reset])

  useEffect(() => () => clearTimersAndControllers(), [clearTimersAndControllers])

  const pollStatus = useCallback((jobId: string) => {
    let active = true
    let consecutiveFailures = 0
    const pollStartTime = Date.now()
    const pollController = new AbortController()

    const check = async () => {
      try {
        if (Date.now() - pollStartTime >= POLL_TIMEOUT_MS) {
          setCompressionState({
            status: 'error',
            error: 'Compression job timed out (；´Д｀)',
            code: ClientErrorCodes.RequestTimeout,
          })
          return
        }

        const job = await getCompressionJob(jobId, pollController.signal)
        if (!active || pollController.signal.aborted) return

        consecutiveFailures = 0

        if (job.status === 'Ready') {
          setCompressionState({ status: 'ready', job })
          return
        }

        if (job.status === 'Failed') {
          setCompressionState({
            status: 'failed',
            job,
            error: job.error || 'Compression job failed on the server',
            code: ClientErrorCodes.CompressionJobFailed,
          })
          return
        }

        setCompressionState({ status: 'polling', job })
        pollTimerRef.current = setTimeout(check, POLL_INTERVAL_MS)
      } catch (err: unknown) {
        if (!active || pollController.signal.aborted) return

        if (Date.now() - pollStartTime >= POLL_TIMEOUT_MS) {
          setCompressionState({
            status: 'error',
            error: 'Compression job timed out (；´Д｀)',
            code: ClientErrorCodes.RequestTimeout,
          })
          return
        }

        consecutiveFailures += 1
        if (consecutiveFailures < MAX_POLL_RETRIES) {
          pollTimerRef.current = setTimeout(check, POLL_INTERVAL_MS)
          return
        }

        const { error, code } = toErrorPayload(err, 'Failed to query compression job status')
        setCompressionState({ status: 'error', error, code })
      }
    }

    void check()

    return () => {
      active = false
      pollController.abort()
      if (pollTimerRef.current) {
        clearTimeout(pollTimerRef.current)
        pollTimerRef.current = null
      }
    }
  }, [])

  const startCompression = useCallback(
    async (request: CompressionRequest) => {
      if (!videoId) return

      clearTimersAndControllers()
      lastRequestRef.current = request

      const controller = new AbortController()
      abortControllerRef.current = controller

      setCompressionState({ status: 'triggering', request })

      try {
        const job = await startCompressionApi(videoId, request, controller.signal)
        if (controller.signal.aborted) return

        if (job.status === 'Ready') {
          setCompressionState({ status: 'ready', job })
          return
        }

        if (job.status === 'Failed') {
          setCompressionState({
            status: 'failed',
            job,
            error: job.error || 'Compression job failed on the server',
            code: ClientErrorCodes.CompressionJobFailed,
          })
          return
        }

        setCompressionState({ status: 'polling', job })
        activePollCleanupRef.current = pollStatus(job.jobId)
      } catch (err: unknown) {
        if (controller.signal.aborted) return
        const { error, code } = toErrorPayload(err, 'Compression request failed')
        setCompressionState({ status: 'error', error, code })
      }
    },
    [videoId, clearTimersAndControllers, pollStatus]
  )

  const retry = useCallback(() => {
    if (lastRequestRef.current) {
      void startCompression(lastRequestRef.current)
    }
  }, [startCompression])

  return { compressionState, startCompression, reset, retry }
}
