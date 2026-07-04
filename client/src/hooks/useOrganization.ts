import { useAuth } from "@clerk/react"
import { useQuery } from "@tanstack/react-query"
import type { OrganizationDetail } from "@/lib/types"

async function fetchOrganization(getToken: () => Promise<string | null>, id: string) {
  const token = await getToken()
  const res = await fetch(`/api/organizations/${id}`, {
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  })
  if (!res.ok) throw new Error("Failed to load organization")
  return res.json() as Promise<OrganizationDetail>
}

export function useOrganization(id: string | undefined) {
  const { getToken } = useAuth()

  return useQuery({
    queryKey: ["organization", id],
    queryFn: () => fetchOrganization(getToken, id!),
    enabled: !!id,
    staleTime: 5 * 60 * 1000,
  })
}
