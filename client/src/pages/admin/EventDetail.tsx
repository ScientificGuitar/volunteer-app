import { useParams, useNavigate } from "react-router-dom"
import { ArrowLeft, Trash2 } from "lucide-react"
import { useEffect, useState, useCallback } from "react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useOrg } from "@/hooks/useOrg"
import { useApi } from "@/hooks/useApi"
import type { RosterEvent } from "@/lib/types"

export function EventDetail() {
  const { id } = useParams<{ id: string }>()
  const { org } = useOrg()
  const api = useApi()
  const navigate = useNavigate()
  const [event, setEvent] = useState<RosterEvent | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const fetchEvent = useCallback(async () => {
    if (!org || !id) return
    setLoading(true)
    setError(null)
    try {
      const data = await api.getRoster(org.id, "2010-01-01")
      const found = data.find((e) => e.id === id)
      if (!found) {
        setError("Event not found")
      } else {
        setEvent(found)
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to load event"
      setError(msg)
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }, [org, id, api])

  useEffect(() => {
    fetchEvent()
  }, [fetchEvent])

  const handleDeleteSignup = async (signupId: string) => {
    try {
      await api.deleteSignup(signupId)
      toast.success("Signup removed")
      await fetchEvent()
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to delete signup"
      toast.error(msg)
    }
  }

  const handleDeleteEvent = async () => {
    if (!id) return
    try {
      await api.deleteEvent(id)
      toast.success("Event deleted")
      navigate("/dashboard")
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to delete event"
      toast.error(msg)
    }
  }

  if (loading) {
    return <div className="py-12 text-center text-muted-foreground">Loading...</div>
  }

  if (error || !event) {
    return (
      <div className="py-12 text-center">
        <p className="mb-4 text-muted-foreground">{error ?? "Event not found"}</p>
        <Button variant="outline" onClick={() => navigate("/dashboard")}>
          Back to Dashboard
        </Button>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-2xl">
      <div className="mb-6 flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => navigate("/dashboard")}>
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div className="flex-1">
          <h1 className="text-2xl font-bold">{event.title}</h1>
          <p className="text-sm text-muted-foreground">{event.date}</p>
        </div>
        <Button variant="destructive" size="sm" onClick={handleDeleteEvent}>
          Delete Event
        </Button>
      </div>

      <div className="space-y-4">
        {event.slots.length === 0 && (
          <p className="text-center text-muted-foreground">No time slots for this event.</p>
        )}
        {event.slots.map((slot) => (
          <Card key={slot.id}>
            <CardHeader className="p-4 pb-0">
              <CardTitle className="flex items-center justify-between text-sm">
                <span>
                  {slot.label}
                  <span className="ml-2 font-normal text-muted-foreground">
                    {slot.startTime}–{slot.endTime}
                  </span>
                </span>
                <Badge
                  variant={slot.signups.length >= slot.capacity ? "destructive" : "secondary"}
                >
                  {slot.signups.length}/{slot.capacity}
                </Badge>
              </CardTitle>
            </CardHeader>
            <CardContent className="p-4 pt-3">
              {slot.signups.length === 0 && (
                <p className="text-sm text-muted-foreground italic">No signups yet</p>
              )}
              {slot.signups.length > 0 && (
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="pb-1 font-medium">Volunteer</th>
                      <th className="pb-1 font-medium">Signed up</th>
                      <th className="w-10 pb-1" />
                    </tr>
                  </thead>
                  <tbody>
                    {slot.signups.map((s) => (
                      <tr key={s.id} className="border-b last:border-0">
                        <td className="py-1">{s.volunteerName}</td>
                        <td className="py-1 text-muted-foreground">
                          {new Date(s.createdAt).toLocaleString()}
                        </td>
                        <td className="py-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-6 w-6 text-muted-foreground hover:text-destructive"
                            onClick={() => handleDeleteSignup(s.id)}
                          >
                            <Trash2 className="h-3 w-3" />
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}
