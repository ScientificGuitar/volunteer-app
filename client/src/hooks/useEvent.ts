import { useQuery } from "@tanstack/react-query"
import { useApi } from "./useApi"

export function useEvent(id: string | undefined) {
  const api = useApi()

  return useQuery({
    queryKey: ["event", id],
    queryFn: () => api.getEvent(id!),
    enabled: !!id,
    staleTime: 5 * 60 * 1000,
  })
}
