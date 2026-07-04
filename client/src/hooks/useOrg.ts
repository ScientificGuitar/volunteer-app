import { useAuth } from "@clerk/react"
import { useQuery } from "@tanstack/react-query"

async function fetchOrg(getToken: () => Promise<string | null>) {
  const token = await getToken()
  const res = await fetch("/api/user/me", {
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  })
  if (!res.ok) return null
  const data = await res.json()
  return data.organization ?? null
}

export function useOrg() {
  const { getToken, isSignedIn } = useAuth()

  const { data: org, isLoading } = useQuery({
    queryKey: ["org"],
    queryFn: () => fetchOrg(getToken),
    enabled: isSignedIn,
    staleTime: 5 * 60 * 1000,
    retry: false,
  })

  return { org: isSignedIn ? (org ?? null) : null, loading: isSignedIn && isLoading }
}
