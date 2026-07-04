export interface UserMeResponse {
  userId: string
  organization: { id: string; name: string } | null
}

export interface OrganizationDetail {
  id: string
  name: string
  createdAt: string
  inviteLinks: InviteLinkInfo[]
}

export interface InviteLinkInfo {
  id: string
  code: string
  isActive: boolean
  createdAt: string
}

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

export interface InviteLinkResponse {
  id: string
  code: string
  isActive: boolean
  createdAt: string
}

export interface CreateInviteLinkResponse {
  id: string
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
