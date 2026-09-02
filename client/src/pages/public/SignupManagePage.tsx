import { useState } from "react"
import { useParams } from "react-router-dom"
import { useQuery } from "@tanstack/react-query"
import { Calendar, Clock, Mail, CalendarX2 } from "lucide-react"
import { toast } from "sonner"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Separator } from "@/components/ui/separator"
import { createPublicApi } from "@/lib/api"

const api = createPublicApi()

export function SignupManagePage() {
  const { token } = useParams<{ token: string }>()
  const [cancelled, setCancelled] = useState(false)
  const [cancelling, setCancelling] = useState(false)

  const { data, isLoading, error } = useQuery({
    queryKey: ["signup-manage", token],
    queryFn: () => api.getSignupDetails(token!),
    enabled: !!token,
    retry: false,
  })

  if (isLoading) {
    return (
      <div className="mx-auto max-w-md py-16 text-center text-muted-foreground">
        Loading...
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="mx-auto max-w-md py-16 text-center">
        <h1 className="mb-2 text-2xl font-bold">Invalid link</h1>
        <p className="text-muted-foreground">
          This signup link is invalid or no longer exists.
        </p>
      </div>
    )
  }

  const isCancelled = cancelled || data.status === "Cancelled"

  const handleCancel = async () => {
    setCancelling(true)
    try {
      await api.cancelSignup(token!)
      setCancelled(true)
      toast.success("Your signup has been cancelled.")
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to cancel signup")
    } finally {
      setCancelling(false)
    }
  }

  return (
    <div className="mx-auto max-w-md space-y-4 py-8">
      <Card>
        <CardHeader className="p-6 pb-3">
          <CardTitle className="text-xl">Your signup</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4 p-6 pt-2">
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">{data.organizationName}</p>
            <p className="text-lg font-semibold">{data.eventTitle}</p>
          </div>

          <Separator />

          <div className="space-y-2 text-sm">
            <p className="flex items-center gap-2">
              <Calendar className="h-4 w-4 text-muted-foreground" />
              {formatDate(data.eventDate)}
            </p>
            <p className="flex items-center gap-2">
              <Clock className="h-4 w-4 text-muted-foreground" />
              {formatTime(data.startTime)}&ndash;{formatTime(data.endTime)}
            </p>
            <p className="flex items-center gap-2">
              <Mail className="h-4 w-4 text-muted-foreground" />
              {data.email}
            </p>
          </div>

          <Separator />

          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">Status</span>
            <Badge variant={isCancelled ? "destructive" : "default"}>
              {isCancelled ? "Cancelled" : "Confirmed"}
            </Badge>
          </div>

          {isCancelled ? (
            <div className="flex items-center gap-2 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-300">
              <CalendarX2 className="h-4 w-4" />
              You&rsquo;ve cancelled this signup. Your spot has been released.
            </div>
          ) : (
            <Button
              variant="destructive"
              className="w-full"
              disabled={cancelling}
              onClick={handleCancel}
            >
              {cancelling ? "Cancelling..." : "Cancel signup"}
            </Button>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function formatTime(value: string): string {
  return value.length >= 5 ? value.slice(0, 5) : value
}

function formatDate(iso: string): string {
  const [year, month, day] = iso.split("-").map(Number)
  if (!year || !month || !day) return iso
  const date = new Date(Date.UTC(year, month - 1, day))
  return date.toLocaleDateString(undefined, {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
    timeZone: "UTC",
  })
}