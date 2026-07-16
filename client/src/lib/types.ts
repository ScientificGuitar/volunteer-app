export interface RosterEvent {
  id: string
  title: string
  description: string | null
  date: string
  slots: RosterSlot[]
}

export interface RosterSlot {
  id: string
  label: string
  startTime: string
  endTime: string
  capacity: number
  signups: SignupInfo[]
}

export interface SignupInfo {
  id: string
  slotId: string
  volunteerName: string
  createdAt: string
}

export interface InviteLink {
  id: string
  eventId: string | null
  code: string
  isActive: boolean
  createdAt: string
}

export interface CreateInviteLinkResponse {
  id: string
  eventId: string | null
  code: string
  isActive: boolean
  createdAt: string
}

export interface CreateEventRequest {
  title: string
  description?: string
  date: string
  slots?: CreateSlotRequest[]
}

export interface CreateSlotRequest {
  label: string
  startTime: string
  endTime: string
  capacity: number
}

export interface PublicInviteData {
  organizationId: string
  organizationName: string
  event: PublicEvent | null
}

export interface PublicEvent {
  id: string
  title: string
  description: string | null
  date: string
  slots: PublicSlot[]
}

export interface PublicSlot {
  id: string
  label: string
  startTime: string
  endTime: string
  capacity: number
  signupCount: number
  isFull: boolean
}
