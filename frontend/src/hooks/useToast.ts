import { useContext } from 'react'
import { ToastContext, type ToastContextValue } from '../context/ToastContext'

function useToast(): ToastContextValue {
    return useContext(ToastContext)
}

export { useToast }