import { useState } from "react"
import { useParams, useNavigate } from "react-router-dom"
import { useQueryClient } from "@tanstack/react-query"
import { toast } from "sonner"
import { Plus, Pencil, Trash2, X } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Badge } from "@/components/ui/badge"
import { useEvent } from "@/hooks/useEvent"
import { useApi } from "@/hooks/useApi"
import type { RosterEvent, RosterSlot } from "@/lib/types"

interface SlotFormData {
  label: string
  startTime: string
  endTime: string
  capacity: number
}

export function EditEvent() {
  const { id } = useParams<{ id: string }>()
  const { data: event, isPending, error } = useEvent(id)
  const navigate = useNavigate()

  if (isPending && !event) {
    return (
      <div className="py-12 text-center text-muted-foreground">Loading...</div>
    )
  }

  if (error || !event) {
    return (
      <div className="py-12 text-center">
        <p className="mb-4 text-muted-foreground">
          {error instanceof Error ? error.message : "Event not found"}
        </p>
        <Button variant="outline" onClick={() => navigate("/dashboard")}>
          Back to Dashboard
        </Button>
      </div>
    )
  }

  return <EventForm event={event} eventId={id!} />
}

interface EventFormProps {
  event: RosterEvent
  eventId: string
}

function EventForm({ event, eventId }: EventFormProps) {
  const api = useApi()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [title, setTitle] = useState(event.title)
  const [description, setDescription] = useState(event.description ?? "")
  const [date, setDate] = useState(event.date)
  const [submitting, setSubmitting] = useState(false)

  const [editingSlotId, setEditingSlotId] = useState<string | null>(null)
  const [slotForm, setSlotForm] = useState<SlotFormData>({
    label: "",
    startTime: "08:00",
    endTime: "09:00",
    capacity: 1,
  })
  const [showAddSlot, setShowAddSlot] = useState(false)
  const [slotSubmitting, setSlotSubmitting] = useState(false)

  const invalidateEvent = () =>
    queryClient.invalidateQueries({ queryKey: ["event", eventId] })

  const validateSlotTimes = () => {
    if (slotForm.endTime <= slotForm.startTime) {
      toast.error("End time must be after start time")
      return false
    }
    return true
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)

    try {
      await api.updateEvent(eventId, {
        title,
        description: description || null,
        date,
      })
      await invalidateEvent()
      toast.success("Event updated")
      navigate(`/events/${eventId}`)
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to update event"
      toast.error(msg)
    } finally {
      setSubmitting(false)
    }
  }

  const handleAddSlot = async () => {
    if (!validateSlotTimes()) return
    setSlotSubmitting(true)

    try {
      await api.createSlot(eventId, slotForm)
      await invalidateEvent()
      toast.success("Slot added")
      setSlotForm({
        label: "",
        startTime: "08:00",
        endTime: "09:00",
        capacity: 1,
      })
      setShowAddSlot(false)
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to add slot"
      toast.error(msg)
    } finally {
      setSlotSubmitting(false)
    }
  }

  const handleUpdateSlot = async (slotId: string) => {
    if (!validateSlotTimes()) return
    setSlotSubmitting(true)

    try {
      await api.updateSlot(eventId, slotId, slotForm)
      await invalidateEvent()
      toast.success("Slot updated")
      setEditingSlotId(null)
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to update slot"
      toast.error(msg)
    } finally {
      setSlotSubmitting(false)
    }
  }

  const handleDeleteSlot = async (slot: RosterSlot) => {
    const confirmMsg =
      slot.signups.length > 0
        ? `Delete "${slot.label}"? This will also remove ${slot.signups.length} signup(s).`
        : `Delete "${slot.label}"?`
    if (!confirm(confirmMsg)) return

    setSlotSubmitting(true)
    try {
      await api.deleteSlot(eventId, slot.id)
      await invalidateEvent()
      toast.success("Slot deleted")
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to delete slot"
      toast.error(msg)
    } finally {
      setSlotSubmitting(false)
    }
  }

  const startEditSlot = (slot: RosterSlot) => {
    setEditingSlotId(slot.id)
    setSlotForm({
      label: slot.label,
      startTime: slot.startTime.slice(0, 5),
      endTime: slot.endTime.slice(0, 5),
      capacity: slot.capacity,
    })
    setShowAddSlot(false)
  }

  const startAddSlot = () => {
    setSlotForm({
      label: "",
      startTime: "08:00",
      endTime: "09:00",
      capacity: 1,
    })
    setShowAddSlot(true)
    setEditingSlotId(null)
  }

  const cancelSlotForm = () => {
    setEditingSlotId(null)
    setShowAddSlot(false)
  }

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <div>
        <h1 className="mb-6 text-2xl font-bold">Edit Event</h1>
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

          <div className="flex gap-2">
            <Button type="submit" disabled={submitting}>
              {submitting ? "Saving..." : "Save Changes"}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => navigate(`/events/${eventId}`)}
            >
              Cancel
            </Button>
          </div>
        </form>
      </div>

      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold">Time Slots</h2>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={startAddSlot}
            disabled={showAddSlot || editingSlotId !== null}
          >
            <Plus className="mr-1 h-4 w-4" />
            Add Slot
          </Button>
        </div>

        {event.slots.length === 0 && !showAddSlot && (
          <p className="text-sm text-muted-foreground">
            No time slots yet. Add time slots that volunteers can sign up for.
          </p>
        )}

        <div className="space-y-3">
          {event.slots.map((slot) => (
            <div key={slot.id} className="rounded-md border p-3">
              {editingSlotId === slot.id ? (
                <SlotEditForm
                  form={slotForm}
                  setForm={setSlotForm}
                  submitting={slotSubmitting}
                  onSave={() => handleUpdateSlot(slot.id)}
                  onCancel={cancelSlotForm}
                />
              ) : (
                <div className="flex items-center justify-between">
                  <div>
                    <span className="font-medium">{slot.label}</span>
                    <span className="ml-2 text-sm text-muted-foreground">
                      {slot.startTime.slice(0, 5)}&ndash;
                      {slot.endTime.slice(0, 5)}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge
                      variant={
                        slot.signups.length >= slot.capacity
                          ? "destructive"
                          : "secondary"
                      }
                    >
                      {slot.signups.length}/{slot.capacity}
                    </Badge>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 text-muted-foreground hover:text-foreground"
                      onClick={() => startEditSlot(slot)}
                      disabled={slotSubmitting}
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 text-muted-foreground hover:text-destructive"
                      onClick={() => handleDeleteSlot(slot)}
                      disabled={slotSubmitting}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              )}
            </div>
          ))}

          {showAddSlot && (
            <div className="rounded-md border border-dashed p-3">
              <SlotEditForm
                form={slotForm}
                setForm={setSlotForm}
                submitting={slotSubmitting}
                onSave={handleAddSlot}
                onCancel={cancelSlotForm}
                isNew
              />
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

interface SlotEditFormProps {
  form: SlotFormData
  setForm: (form: SlotFormData) => void
  submitting: boolean
  onSave: () => void
  onCancel: () => void
  isNew?: boolean
}

function SlotEditForm({
  form,
  setForm,
  submitting,
  onSave,
  onCancel,
  isNew,
}: SlotEditFormProps) {
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    onSave()
  }

  const updateField = <K extends keyof SlotFormData>(
    field: K,
    value: SlotFormData[K]
  ) => {
    setForm({ ...form, [field]: value })
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-wrap items-end gap-2">
      <div className="flex-1 space-y-1">
        <Label className="text-xs">Label</Label>
        <Input
          value={form.label}
          onChange={(e) => updateField("label", e.target.value)}
          placeholder="Morning"
          required
          className="h-8 text-sm"
        />
      </div>
      <div className="w-20 space-y-1">
        <Label className="text-xs">Start</Label>
        <Input
          type="time"
          value={form.startTime}
          onChange={(e) => updateField("startTime", e.target.value)}
          required
          className="h-8 text-sm"
        />
      </div>
      <div className="w-20 space-y-1">
        <Label className="text-xs">End</Label>
        <Input
          type="time"
          value={form.endTime}
          onChange={(e) => updateField("endTime", e.target.value)}
          required
          className="h-8 text-sm"
        />
      </div>
      <div className="w-16 space-y-1">
        <Label className="text-xs">Cap</Label>
        <Input
          type="number"
          min={1}
          value={form.capacity}
          onChange={(e) =>
            updateField("capacity", parseInt(e.target.value) || 1)
          }
          required
          className="h-8 text-sm"
        />
      </div>
      <Button type="submit" size="sm" disabled={submitting}>
        {submitting ? "..." : isNew ? "Add" : "Save"}
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="h-8 w-8"
        onClick={onCancel}
      >
        <X className="h-4 w-4" />
      </Button>
    </form>
  )
}
