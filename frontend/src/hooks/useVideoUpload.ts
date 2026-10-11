import { useState, useCallback, useRef, useEffect } from 'react'
import {
  uploadVideo,
  getVideoStatus,
  type VideoResponse,
  type VideoStatus,
} from '../api/videoApi'

export type UploadState =
  | { status: 'idle' }
  | { status: 'uploading'; fileName: string; sizeBytes: number; progress: number }
  | {
      status: 'polling'
      id: string
      fileName: string
      sizeBytes: number
      videoStatus: VideoStatus
      video?: VideoResponse
    }
  | { status: 'ready'; video: VideoResponse }
  | { status: 'failed'; fileName: string; sizeBytes: number; error: string }
  | { status: 'error'; fileName: string; sizeBytes: number; error: string }

export interface UseVideoUploadResult {
  uploadState: UploadState
  startUpload: (file: File) => Promise<void>
  reset: () => void
  retry: () => void
}

const POLL_INTERVAL_MS = 2000

export function useVideoUpload(): UseVideoUploadResult {
  const [uploadState, setUploadState] = useState<UploadState>({ status: 'idle' })
  const abortControllerRef = useRef<AbortController | null>(null)
  const pollTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const lastFileRef = useRef<File | null>(null)

  const clearTimersAndControllers = useCallback(() => {
    if (pollTimerRef.current) {
      clearTimeout(pollTimerRef.current)
      pollTimerRef.current = null
    }
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
      abortControllerRef.current = null
    }
  }, [])

  useEffect(() => {
    return () => {
      clearTimersAndControllers()
    }
  }, [clearTimersAndControllers])

  const pollStatus = useCallback((id: string, fileName: string, sizeBytes: number) => {
    let active = true

    const check = async () => {
      try {
        const video = await getVideoStatus(id)
        if (!active) return

        if (video.status === 'Ready') {
          setUploadState({ status: 'ready', video })
          return
        }

        if (video.status === 'Failed') {
          setUploadState({
            status: 'failed',
            fileName,
            sizeBytes,
            error: 'Processing failed on the server',
          })
          return
        }

        // Uploaded or Processing
        setUploadState({
          status: 'polling',
          id,
          fileName,
          sizeBytes,
          videoStatus: video.status,
          video,
        })

        pollTimerRef.current = setTimeout(check, POLL_INTERVAL_MS)
      } catch (err: unknown) {
        if (!active) return
        const message = err instanceof Error ? err.message : 'Failed to query video processing status'
        setUploadState({
          status: 'error',
          fileName,
          sizeBytes,
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

  const startUpload = useCallback(
    async (file: File) => {
      clearTimersAndControllers()
      lastFileRef.current = file

      const controller = new AbortController()
      abortControllerRef.current = controller

      setUploadState({
        status: 'uploading',
        fileName: file.name,
        sizeBytes: file.size,
        progress: 0,
      })

      try {
        const response = await uploadVideo(file, {
          onProgress: (percent) => {
            setUploadState((prev) =>
              prev.status === 'uploading' ? { ...prev, progress: percent } : prev
            )
          },
          signal: controller.signal,
        })

        if (controller.signal.aborted) return

        // Set to polling immediately and begin checking
        const initialStatus = response.status === 'Processing' ? 'Processing' : 'Uploaded'
        setUploadState({
          status: 'polling',
          id: response.id,
          fileName: response.fileName,
          sizeBytes: response.sizeBytes,
          videoStatus: initialStatus,
        })

        pollStatus(response.id, response.fileName, response.sizeBytes)
      } catch (err: unknown) {
        if (controller.signal.aborted) return
        const message = err instanceof Error ? err.message : 'Upload failed'
        setUploadState({
          status: 'error',
          fileName: file.name,
          sizeBytes: file.size,
          error: message,
        })
      }
    },
    [clearTimersAndControllers, pollStatus]
  )

  const reset = useCallback(() => {
    clearTimersAndControllers()
    lastFileRef.current = null
    setUploadState({ status: 'idle' })
  }, [clearTimersAndControllers])

  const retry = useCallback(() => {
    if (lastFileRef.current) {
      void startUpload(lastFileRef.current)
    }
  }, [startUpload])

  return { uploadState, startUpload, reset, retry }
}
