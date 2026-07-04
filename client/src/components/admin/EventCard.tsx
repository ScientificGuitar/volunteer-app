import { Trash2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import type { RosterEvent } from "@/lib/types"

interface EventCardProps {
  event: RosterEvent
  onDeleteSignup: (signupId: string) => void
}

export function EventCard({ event, onDeleteSignup }: EventCardProps) {
  return (
    <Card>
      <CardHeader className="p-3 pb-0">
        <CardTitle className="text-sm font-semibold">{event.title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-2 p-3 pt-2">
        {event.slots.length === 0 && (
          <p className="text-xs text-muted-foreground">No slots</p>
        )}
        {event.slots.map((slot) => (
          <div key={slot.id} className="rounded-md border p-2 text-xs">
            <div className="mb-1 flex items-center justify-between">
              <span className="font-medium">
                {slot.label}
                <span className="ml-1 text-muted-foreground">
                  ({slot.startTime}–{slot.endTime})
                </span>
              </span>
              <Badge
                variant={slot.signups.length >= slot.capacity ? "destructive" : "secondary"}
                className="text-[10px]"
              >
                {slot.signups.length}/{slot.capacity}
              </Badge>
            </div>
            {slot.signups.length === 0 && (
              <p className="text-muted-foreground italic">No signups yet</p>
            )}
            {slot.signups.length > 0 && (
              <ul className="space-y-0.5">
                {slot.signups.map((s) => (
                  <li key={s.id} className="flex items-center justify-between">
                    <span>{s.volunteerName}</span>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-4 w-4 text-muted-foreground hover:text-destructive"
                      onClick={() => onDeleteSignup(s.id)}
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        ))}
      </CardContent>
    </Card>
  )
}
