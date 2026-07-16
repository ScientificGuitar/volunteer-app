import { useState } from "react"
import { useNavigate } from "react-router-dom"
import { toast } from "sonner"
import { Plus, X } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useOrg } from "@/hooks/useOrg"
import { useApi } from "@/hooks/useApi"

interface SlotRow {
  key: number
  label: string
  startTime: string
  endTime: string
  capacity: number
}

let nextKey = 1

export function CreateEvent() {
  const { org } = useOrg()
  const api = useApi()
  const navigate = useNavigate()
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [date, setDate] = useState("")
  const [slots, setSlots] = useState<SlotRow[]>([])
  const [submitting, setSubmitting] = useState(false)

  if (!org) return null

  const addSlot = () => {
    setSlots((prev) => [
      ...prev,
      { key: nextKey++, label: "", startTime: "08:00", endTime: "09:00", capacity: 1 },
    ])
  }

  const removeSlot = (key: number) => {
    setSlots((prev) => prev.filter((s) => s.key !== key))
  }

  const updateSlot = (key: number, field: keyof SlotRow, value: string | number) => {
    setSlots((prev) =>
      prev.map((s) => (s.key === key ? { ...s, [field]: value } : s))
    )
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!org) return
    setSubmitting(true)

    try {
      await api.createEvent(org.id, {
        title,
        description: description || null,
        date,
        slots: slots.length > 0
          ? slots.map((s) => ({
              label: s.label,
              startTime: s.startTime,
              endTime: s.endTime,
              capacity: s.capacity,
            }))
          : null,
      })
      toast.success("Event created")
      navigate("/dashboard")
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to create event"
      toast.error(msg)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="mx-auto max-w-lg">
      <h1 className="mb-6 text-2xl font-bold">Create Event</h1>
      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="space-y-2">
          <Label htmlFor="title">Event Title</Label>
          <Input
            id="title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            required
            placeholder="Sunday Service"
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="description">Description (optional)</Label>
          <Input
            id="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Weekly Sunday service"
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="date">Date</Label>
          <Input
            id="date"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            required
          />
        </div>

        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <Label>Time Slots</Label>
            <Button type="button" variant="outline" size="sm" onClick={addSlot}>
              <Plus className="mr-1 h-4 w-4" /> Add Slot
            </Button>
          </div>

          {slots.length === 0 && (
            <p className="text-sm text-muted-foreground">
              No slots yet. Add time slots that volunteers can sign up for.
            </p>
          )}

          {slots.map((slot) => (
            <div key={slot.key} className="flex flex-wrap items-end gap-2 rounded-md border p-3">
              <div className="flex-1 space-y-1">
                <Label className="text-xs">Label</Label>
                <Input
                  value={slot.label}
                  onChange={(e) => updateSlot(slot.key, "label", e.target.value)}
                  placeholder="Morning"
                  required
                  className="h-8 text-sm"
                />
              </div>
              <div className="w-20 space-y-1">
                <Label className="text-xs">Start</Label>
                <Input
                  type="time"
                  value={slot.startTime}
                  onChange={(e) => updateSlot(slot.key, "startTime", e.target.value)}
                  required
                  className="h-8 text-sm"
                />
              </div>
              <div className="w-20 space-y-1">
                <Label className="text-xs">End</Label>
                <Input
                  type="time"
                  value={slot.endTime}
                  onChange={(e) => updateSlot(slot.key, "endTime", e.target.value)}
                  required
                  className="h-8 text-sm"
                />
              </div>
              <div className="w-16 space-y-1">
                <Label className="text-xs">Cap</Label>
                <Input
                  type="number"
                  min={1}
                  value={slot.capacity}
                  onChange={(e) =>
                    updateSlot(slot.key, "capacity", parseInt(e.target.value) || 1)
                  }
                  required
                  className="h-8 text-sm"
                />
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-muted-foreground hover:text-destructive"
                onClick={() => removeSlot(slot.key)}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </div>

        <div className="flex gap-2">
          <Button type="submit" disabled={submitting}>
            {submitting ? "Creating..." : "Create Event"}
          </Button>
          <Button type="button" variant="outline" onClick={() => navigate("/dashboard")}>
            Cancel
          </Button>
        </div>
      </form>
    </div>
  )
}
