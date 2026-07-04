import { useAuth } from "@clerk/react"
import { useQuery } from "@tanstack/react-query"
import type { RosterEvent } from "@/lib/types"

async function fetchEvent(getToken: () => Promise<string | null>, id: string) {
  const token = await getToken()
  const res = await fetch(`/api/events/${id}`, {
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  })
  if (!res.ok) throw new Error("Failed to load event")
  return res.json() as Promise<RosterEvent>
}

export function useEvent(id: string | undefined) {
  const { getToken } = useAuth()

  return useQuery({
    queryKey: ["event", id],
    queryFn: () => fetchEvent(getToken, id!),
    enabled: !!id,
    staleTime: 30_000,
  })
}
