import { useMutation, useQueryClient } from "@tanstack/react-query"
import { toast } from "sonner"
import { useApi } from "./useApi"

export function useDeleteSignup() {
  const api = useApi()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (signupId: string) => api.deleteSignup(signupId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["roster"] })
    },
    onError: (e: Error) => {
      toast.error(e.message)
    },
  })
}
