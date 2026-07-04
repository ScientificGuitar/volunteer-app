import { useAuth } from "@clerk/react"
import { useMemo } from "react"
import { createAdminApi } from "@/lib/api"

export function useApi() {
  const { getToken } = useAuth()
  return useMemo(() => createAdminApi(getToken), [getToken])
}
