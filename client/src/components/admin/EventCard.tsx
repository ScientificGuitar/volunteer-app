import { Link } from "react-router-dom"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { activeSignupCount, formatTime } from "@/lib/utils"
import type { RosterEvent } from "@/lib/types"

interface EventCardProps {
  event: RosterEvent
}

export function EventCard({ event }: EventCardProps) {
  return (
    <Card size="sm">
      <CardHeader className="pb-0">
        <CardTitle className="text-sm font-semibold">
          <Link to={`/events/${event.id}`} className="hover:underline">
            {event.title}
          </Link>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {event.slots.length === 0 && (
          <p className="text-xs text-muted-foreground">No slots</p>
        )}
        {event.slots.map((slot) => {
          const count = activeSignupCount(slot.signups)
          return (
            <div key={slot.id} className="rounded-md border p-2 text-xs">
              <div className="flex items-start justify-between">
                <div>
                  <div className="font-medium">{slot.label}</div>
                  <div className="text-muted-foreground">
                    {formatTime(slot.startTime)}–{formatTime(slot.endTime)}
                  </div>
                </div>
                <Badge
                  variant={count >= slot.capacity ? "destructive" : "secondary"}
                  className="text-[10px]"
                >
                  {count}/{slot.capacity}
                </Badge>
              </div>
            </div>
          )
        })}
      </CardContent>
    </Card>
  )
}
