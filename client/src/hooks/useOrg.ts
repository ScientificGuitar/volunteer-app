import { useAuth } from "@clerk/react"
import { useEffect, useState } from "react"

export function useOrg() {
  const { getToken, isSignedIn } = useAuth()
  const [org, setOrg] = useState<{ id: string; name: string } | null | undefined>(
    isSignedIn ? undefined : null
  )

  useEffect(() => {
    if (!isSignedIn) return

    let cancelled = false

    const load = async () => {
      const token = await getToken()
      const res = await globalThis.fetch("/api/user/me", {
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
      })
      if (cancelled) return
      if (!res.ok) {
        setOrg(null)
        return
      }
      const data = await res.json()
      setOrg(data.organization ?? null)
    }

    load()
    return () => {
      cancelled = true
      setOrg(null)
    }
  }, [getToken, isSignedIn])

  return { org, loading: org === undefined }
}
