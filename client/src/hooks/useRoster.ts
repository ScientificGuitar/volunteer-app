import { useAuth } from "@clerk/react"
import { useQuery } from "@tanstack/react-query"
import type { RosterEvent } from "@/lib/types"

async function fetchRoster(getToken: () => Promise<string | null>, orgId: string, weekStart: string) {
  const token = await getToken()
  const res = await fetch(`/api/organizations/${orgId}/roster?weekStart=${weekStart}`, {
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  })
  if (!res.ok) throw new Error("Failed to load roster")
  return res.json() as Promise<RosterEvent[]>
}

export function useRoster(orgId: string, weekStart: string) {
  const { getToken } = useAuth()

  return useQuery({
    queryKey: ["roster", orgId, weekStart],
    queryFn: () => fetchRoster(getToken, orgId, weekStart),
    staleTime: 30_000,
  })
}
