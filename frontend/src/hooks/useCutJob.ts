import { useState, useCallback, useRef, useEffect } from 'react'
import {
  startCut as startCutApi,
  getCutJob,
  type CutRequest,
  type CutJobResponse,
} from '../api/cutApi'
import { ClientErrorCodes, toErrorPayload } from '../api/apiError'

export type CutJobState =
  | { status: 'idle' }
  | { status: 'triggering'; request: CutRequest }
  | { status: 'polling'; job: CutJobResponse }
  | { status: 'ready'; job: CutJobResponse }
  | { status: 'failed'; job: CutJobResponse; error: string; code: string }
  | { status: 'error'; error: string; code: string }

export interface UseCutJobResult {
  cutState: CutJobState
  startCut: (request: CutRequest) => Promise<void>
  reset: () => void
  retry: () => void
}

const POLL_INTERVAL_MS = 1000
const MAX_POLL_RETRIES = 3
const POLL_TIMEOUT_MS = 300000

/** Manages triggering a cut job and polling its status until Ready or Failed. */
export function useCutJob(videoId: string | null): UseCutJobResult {
  const [cutState, setCutState] = useState<CutJobState>({ status: 'idle' })
  const abortControllerRef = useRef<AbortController | null>(null)
  const pollTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const activePollCleanupRef = useRef<(() => void) | null>(null)
  const lastRequestRef = useRef<CutRequest | null>(null)

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
    setCutState({ status: 'idle' })
  }, [clearTimersAndControllers])

  const currentVideoIdRef = useRef(videoId)
  useEffect(() => {
    if (currentVideoIdRef.current !== videoId) {
      currentVideoIdRef.current = videoId
      reset()
    }
  }, [videoId, reset])

  useEffect(() => {
    return () => {
      clearTimersAndControllers()
    }
  }, [clearTimersAndControllers])

  const pollStatus = useCallback((jobId: string) => {
    let active = true
    let consecutiveFailures = 0
    const pollStartTime = Date.now()

    const check = async () => {
      try {
        if (Date.now() - pollStartTime >= POLL_TIMEOUT_MS) {
          setCutState({
            status: 'error',
            error: 'Cut job timed out (；´Д｀)',
            code: ClientErrorCodes.RequestTimeout,
          })
          return
        }

        const job = await getCutJob(jobId)
        if (!active) return

        consecutiveFailures = 0

        if (job.status === 'Ready') {
          setCutState({ status: 'ready', job })
          return
        }

        if (job.status === 'Failed') {
          setCutState({
            status: 'failed',
            job,
            error: job.error || 'Cut job failed on the server',
            code: ClientErrorCodes.CutJobFailed,
          })
          return
        }

        setCutState({ status: 'polling', job })
        pollTimerRef.current = setTimeout(check, POLL_INTERVAL_MS)
      } catch (err: unknown) {
        if (!active) return

        if (Date.now() - pollStartTime >= POLL_TIMEOUT_MS) {
          setCutState({
            status: 'error',
            error: 'Cut job timed out (；´Д｀)',
            code: ClientErrorCodes.RequestTimeout,
          })
          return
        }

        consecutiveFailures += 1
        if (consecutiveFailures < MAX_POLL_RETRIES) {
          pollTimerRef.current = setTimeout(check, POLL_INTERVAL_MS)
          return
        }

        const { error, code } = toErrorPayload(err, 'Failed to query cut job status')
        setCutState({ status: 'error', error, code })
      }
    }

    void check()

    return () => {
      active = false
      if (pollTimerRef.current) {
        clearTimeout(pollTimerRef.current)
        pollTimerRef.current = null
      }
    }
  }, [])

  const startCut = useCallback(
    async (request: CutRequest) => {
      if (!videoId) return

      clearTimersAndControllers()
      lastRequestRef.current = request

      const controller = new AbortController()
      abortControllerRef.current = controller

      setCutState({ status: 'triggering', request })

      try {
        const job = await startCutApi(videoId, request, controller.signal)
        if (controller.signal.aborted) return

        if (job.status === 'Ready') {
          setCutState({ status: 'ready', job })
          return
        }

        if (job.status === 'Failed') {
          setCutState({
            status: 'failed',
            job,
            error: job.error || 'Cut job failed on the server',
            code: ClientErrorCodes.CutJobFailed,
          })
          return
        }

        setCutState({ status: 'polling', job })
        activePollCleanupRef.current = pollStatus(job.jobId)
      } catch (err: unknown) {
        if (controller.signal.aborted) return
        const { error, code } = toErrorPayload(err, 'Cut request failed')
        setCutState({ status: 'error', error, code })
      }
    },
    [videoId, clearTimersAndControllers, pollStatus]
  )

  const retry = useCallback(() => {
    if (lastRequestRef.current) {
      void startCut(lastRequestRef.current)
    }
  }, [startCut])

  return { cutState, startCut, reset, retry }
}
