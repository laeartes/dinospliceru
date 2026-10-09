import { useCallback, useEffect, useState } from 'react'
import { fetchConfig, type VideoUploadConfig } from '../api/configApi'

type UploadConfigState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; config: VideoUploadConfig }

type UseUploadConfigResult = UploadConfigState & { retry: () => void }

function useUploadConfig(): UseUploadConfigResult {
  const [state, setState] = useState<UploadConfigState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    fetchConfig(controller.signal)
      .then((config) => setState({ status: 'ready', config: config.videoUpload }))
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'error' })
        }
      })

    return () => controller.abort()
  }, [attempt])

  const retry = useCallback(() => {
    setState({ status: 'loading' })
    setAttempt((current) => current + 1)
  }, [])

  return { ...state, retry }
}

export { useUploadConfig }
