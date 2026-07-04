import { useAuth } from "@clerk/react"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { toast } from "sonner"

async function deleteSignup(getToken: () => Promise<string | null>, signupId: string) {
  const token = await getToken()
  const res = await fetch(`/api/signups/${signupId}`, {
    method: "DELETE",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
  })
  if (!res.ok) throw new Error("Failed to delete signup")
}

export function useDeleteSignup() {
  const { getToken } = useAuth()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (signupId: string) => deleteSignup(getToken, signupId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["roster"] })
    },
    onError: (e: Error) => {
      toast.error(e.message)
    },
  })
}
