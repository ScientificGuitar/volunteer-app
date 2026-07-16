import { useQuery } from "@tanstack/react-query"
import { useApi } from "./useApi"

export function useRoster(orgId: string, weekStart: string) {
  const api = useApi()

  return useQuery({
    queryKey: ["roster", orgId, weekStart],
    queryFn: () => api.getRoster(orgId, weekStart),
    staleTime: 5 * 60 * 1000,
  })
}
