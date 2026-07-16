import { useState } from "react"
import { useParams } from "react-router-dom"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { Calendar, Users, CheckCircle2 } from "lucide-react"
import { toast } from "sonner"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { createPublicApi } from "@/lib/api"
import type { PublicSlot } from "@/lib/types"

const api = createPublicApi()

export function InvitePage() {
  const { code } = useParams<{ code: string }>()
  const queryClient = useQueryClient()

  const { data, isLoading, error } = useQuery({
    queryKey: ["invite", code],
    queryFn: () => api.getInvitePage(code!),
    enabled: !!code,
    retry: false,
  })

  if (isLoading) {
    return (
      <div className="mx-auto max-w-2xl py-12 text-center text-muted-foreground">
        Loading...
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="mx-auto max-w-md py-16 text-center">
        <h1 className="mb-2 text-2xl font-bold">Invalid invite link</h1>
        <p className="text-muted-foreground">
          This invite link is invalid, has been revoked, or no longer points to
          an active event.
        </p>
      </div>
    )
  }

  const { organizationName, event } = data

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <header className="space-y-1 text-center">
        <p className="text-sm font-medium text-muted-foreground">
          {organizationName}
        </p>
        <h1 className="text-3xl font-bold">{event.title}</h1>
        <div className="flex flex-wrap items-center justify-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
          <span className="flex items-center gap-1">
            <Calendar className="h-4 w-4" />
            {formatDate(event.date)}
          </span>
        </div>
        {event.description && (
          <p className="mx-auto mt-2 max-w-prose text-sm text-muted-foreground">
            {event.description}
          </p>
        )}
      </header>

      <section className="space-y-3">
        <h2 className="flex items-center gap-2 text-lg font-semibold">
          <Users className="h-5 w-5" />
          Volunteer slots
        </h2>
        {event.slots.length === 0 && (
          <p className="rounded-md border border-dashed py-8 text-center text-sm text-muted-foreground">
            No slots have been created for this event yet.
          </p>
        )}
        {event.slots.map((slot) => (
          <SlotCard
            key={slot.id}
            slot={slot}
            onSignUp={async (volunteerName) => {
              await api.createSignup(code!, { slotId: slot.id, volunteerName })
              await queryClient.invalidateQueries({
                queryKey: ["invite", code],
              })
            }}
          />
        ))}
      </section>
    </div>
  )
}

interface SlotCardProps {
  slot: PublicSlot
  onSignUp: (volunteerName: string) => Promise<void>
}

function SlotCard({ slot, onSignUp }: SlotCardProps) {
  const [name, setName] = useState("")
  const [submitting, setSubmitting] = useState(false)
  const [signedUp, setSignedUp] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    const trimmed = name.trim()
    if (!trimmed) {
      toast.error("Please enter your name")
      return
    }
    setSubmitting(true)
    try {
      await onSignUp(trimmed)
      setSignedUp(true)
      toast.success(`You're signed up for ${slot.label}`)
    } catch (err) {
      if (err instanceof Error && err.message.toLowerCase().includes("full")) {
        toast.error("This slot just filled up — please pick another.")
      } else {
        toast.error(err instanceof Error ? err.message : "Sign up failed")
      }
    } finally {
      setSubmitting(false)
    }
  }

  const percent =
    slot.capacity > 0
      ? Math.min(100, (slot.signupCount / slot.capacity) * 100)
      : 0

  return (
    <Card>
      <CardHeader className="p-4 pb-2">
        <CardTitle className="flex items-center justify-between text-base">
          <span>
            {slot.label}
            <span className="ml-2 font-normal text-muted-foreground">
              {slot.startTime}&ndash;{slot.endTime}
            </span>
          </span>
          <Badge variant={slot.isFull ? "destructive" : "secondary"}>
            {slot.signupCount}/{slot.capacity}
          </Badge>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 p-4 pt-2">
        <CapacityBar percent={percent} isFull={slot.isFull} />

        {signedUp ? (
          <div className="flex items-center gap-2 rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700 dark:border-green-900 dark:bg-green-950 dark:text-green-300">
            <CheckCircle2 className="h-4 w-4" />
            You&rsquo;re signed up. See you there!
          </div>
        ) : slot.isFull ? (
          <p className="rounded-md border border-dashed p-3 text-center text-sm text-muted-foreground">
            This slot is full.
          </p>
        ) : (
          <form onSubmit={handleSubmit} className="flex items-center gap-2">
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Your name"
              maxLength={200}
              className="h-9"
              disabled={submitting}
            />
            <Button type="submit" size="sm" disabled={submitting}>
              {submitting ? "Signing up..." : "Sign up"}
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  )
}

function CapacityBar({
  percent,
  isFull,
}: {
  percent: number
  isFull: boolean
}) {
  return (
    <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
      <div
        className={
          isFull
            ? "h-full bg-red-500 transition-all"
            : "h-full bg-primary transition-all"
        }
        style={{ width: `${percent}%` }}
      />
    </div>
  )
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
