import { useEffect } from 'react'

/** "Screen – AdaPlatform" in the browser tab; just the app name without a screen. */
export function useDocumentTitle(screen: string | undefined) {
  useEffect(() => {
    document.title = screen ? `${screen} – AdaPlatform` : 'AdaPlatform'
  }, [screen])
}
