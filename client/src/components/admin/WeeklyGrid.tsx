import { useState, useEffect, useCallback } from "react"
import { toast } from "sonner"
import { ChevronLeft, ChevronRight, RefreshCw } from "lucide-react"
import { Button } from "@/components/ui/button"
import { EventCard } from "@/components/admin/EventCard"
import type { RosterEvent } from "@/lib/types"
import type { AdminApi } from "@/lib/api"

interface WeeklyGridProps {
  orgId: string
  api: AdminApi
}

function getMonday(date: Date): Date {
  const d = new Date(date)
  const day = d.getDay()
  d.setDate(d.getDate() - ((day + 6) % 7))
  d.setHours(0, 0, 0, 0)
  return d
}

function formatDate(d: Date): string {
  return d.toISOString().split("T")[0]
}

function formatDayHeader(d: Date): string {
  const days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]
  return `${days[d.getDay()]} ${d.getMonth() + 1}/${d.getDate()}`
}

function formatWeekRange(monday: Date): string {
  const sunday = new Date(monday)
  sunday.setDate(sunday.getDate() + 6)
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
    "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
  return `${months[monday.getMonth()]} ${monday.getDate()} – ${months[sunday.getMonth()]} ${sunday.getDate()}, ${sunday.getFullYear()}`
}

export function WeeklyGrid({ orgId, api }: WeeklyGridProps) {
  const [monday, setMonday] = useState(() => getMonday(new Date()))
  const [events, setEvents] = useState<RosterEvent[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const days = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(monday)
    d.setDate(d.getDate() + i)
    return { date: formatDate(d), label: formatDayHeader(d) }
  })

  const fetchRoster = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await api.getRoster(orgId, formatDate(monday))
      setEvents(data)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load roster")
    } finally {
      setLoading(false)
    }
  }, [orgId, monday, api])

  useEffect(() => {
    fetchRoster()
  }, [fetchRoster])

  const prevWeek = () => {
    const d = new Date(monday)
    d.setDate(d.getDate() - 7)
    setMonday(d)
  }

  const nextWeek = () => {
    const d = new Date(monday)
    d.setDate(d.getDate() + 7)
    setMonday(d)
  }

  const handleDeleteSignup = async (signupId: string) => {
    try {
      await api.deleteSignup(signupId)
      await fetchRoster()
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to delete signup")
    }
  }

  const eventsByDate = new Map<string, RosterEvent[]>()
  for (const evt of events) {
    const existing = eventsByDate.get(evt.date) ?? []
    existing.push(evt)
    eventsByDate.set(evt.date, existing)
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button variant="outline" size="icon" onClick={prevWeek}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="min-w-[14rem] text-center text-sm font-medium">
            {formatWeekRange(monday)}
          </span>
          <Button variant="outline" size="icon" onClick={nextWeek}>
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
        <Button variant="ghost" size="icon" onClick={fetchRoster}>
          <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
        </Button>
      </div>

      {loading && events.length === 0 && (
        <div className="py-12 text-center text-muted-foreground">Loading roster...</div>
      )}
      {error && (
        <div className="py-12 text-center text-destructive">{error}</div>
      )}
      {!loading && !error && events.length === 0 && (
        <div className="py-12 text-center text-muted-foreground">
          No events this week.{" "}
          <a href="/events/new" className="text-primary hover:underline">
            Create one
          </a>
        </div>
      )}

      <div className="grid grid-cols-7 gap-3">
        {days.map((day) => (
          <div key={day.date}>
            <div className="mb-2 rounded-md bg-muted px-2 py-1 text-center text-xs font-semibold text-muted-foreground">
              {day.label}
            </div>
            <div className="space-y-2">
              {(eventsByDate.get(day.date) ?? []).map((evt) => (
                <EventCard
                  key={evt.id}
                  event={evt}
                  onDeleteSignup={handleDeleteSignup}
                />
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
