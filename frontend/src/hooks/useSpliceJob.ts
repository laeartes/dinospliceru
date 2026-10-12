import { useState, useCallback, useRef, useEffect } from 'react'
import {
  createSpliceJob,
  getSpliceJob,
  type SpliceMode,
  type SpliceRequest,
  type SpliceJobStatus,
  type SpliceSegment,
} from '../api/spliceApi'

export type SpliceJobState =
  | { status: 'idle' }
  | { status: 'triggering'; mode: SpliceMode; value: number }
  | { status: 'polling'; jobId: string; jobStatus: SpliceJobStatus; mode: SpliceMode; value: number }
  | { status: 'ready'; jobId: string; segments: SpliceSegment[] }
  | { status: 'failed'; jobId?: string; error: string }
  | { status: 'error'; error: string }

export interface UseSpliceJobResult {
  spliceState: SpliceJobState
  startSplice: (mode: SpliceMode, value: number) => Promise<void>
  reset: () => void
  retry: () => void
}

const POLL_INTERVAL_MS = 2000
const MAX_POLL_RETRIES = 3

export function useSpliceJob(videoId: string | null): UseSpliceJobResult {
  const [spliceState, setSpliceState] = useState<SpliceJobState>({ status: 'idle' })
  const abortControllerRef = useRef<AbortController | null>(null)
  const pollTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const activePollCleanupRef = useRef<(() => void) | null>(null)
  const lastRequestRef = useRef<SpliceRequest | null>(null)

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
    setSpliceState({ status: 'idle' })
  }, [clearTimersAndControllers])

  // Reset if videoId changes or becomes null
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

  const pollStatus = useCallback((jobId: string, mode: SpliceMode, value: number) => {
    let active = true
    let consecutiveFailures = 0

    const check = async () => {
      try {
        const job = await getSpliceJob(jobId)
        if (!active) return

        consecutiveFailures = 0

        if (job.status === 'Ready') {
          setSpliceState({
            status: 'ready',
            jobId,
            segments: job.segments,
          })
          return
        }

        if (job.status === 'Failed') {
          setSpliceState({
            status: 'failed',
            jobId,
            error: job.error || 'Splice job failed on the server',
          })
          return
        }

        // Status is Pending or Processing
        setSpliceState({
          status: 'polling',
          jobId,
          jobStatus: job.status,
          mode,
          value,
        })

        pollTimerRef.current = setTimeout(check, POLL_INTERVAL_MS)
      } catch (err: unknown) {
        if (!active) return

        consecutiveFailures += 1
        if (consecutiveFailures < MAX_POLL_RETRIES) {
          pollTimerRef.current = setTimeout(check, POLL_INTERVAL_MS)
          return
        }

        const message = err instanceof Error ? err.message : 'Failed to query splice job status'
        setSpliceState({
          status: 'error',
          error: message,
        })
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

  const startSplice = useCallback(
    async (mode: SpliceMode, value: number) => {
      if (!videoId) return

      clearTimersAndControllers()
      lastRequestRef.current = { mode, value }

      const controller = new AbortController()
      abortControllerRef.current = controller

      setSpliceState({
        status: 'triggering',
        mode,
        value,
      })

      try {
        const response = await createSpliceJob(videoId, { mode, value }, controller.signal)
        if (controller.signal.aborted) return

        const initialStatus: SpliceJobStatus =
          response.status === 'Processing' ? 'Processing' : 'Pending'

        setSpliceState({
          status: 'polling',
          jobId: response.jobId,
          jobStatus: initialStatus,
          mode,
          value,
        })

        activePollCleanupRef.current = pollStatus(response.jobId, mode, value)
      } catch (err: unknown) {
        if (controller.signal.aborted) return
        const message = err instanceof Error ? err.message : 'Splice request failed'
        setSpliceState({
          status: 'error',
          error: message,
        })
      }
    },
    [videoId, clearTimersAndControllers, pollStatus]
  )

  const retry = useCallback(() => {
    if (lastRequestRef.current) {
      void startSplice(lastRequestRef.current.mode, lastRequestRef.current.value)
    }
  }, [startSplice])

  return { spliceState, startSplice, reset, retry }
}
