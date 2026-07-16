import { useAuth } from "@clerk/react"
import { useQuery } from "@tanstack/react-query"
import { useApi } from "./useApi"

export function useOrg() {
  const { isSignedIn } = useAuth()
  const api = useApi()

  const { data, isLoading, error } = useQuery({
    queryKey: ["org"],
    queryFn: () => api.getCurrentUser(),
    enabled: isSignedIn,
    staleTime: 5 * 60 * 1000,
    retry: false,
  })

  return {
    org: isSignedIn ? (data?.organization ?? null) : null,
    loading: isSignedIn && isLoading,
    error: isSignedIn ? (error ?? null) : null,
  }
}
