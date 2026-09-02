import type { components } from "@/lib/generated/schema"

type Schemas = components["schemas"]

type Num<T> = number extends T ? number : T

type NumericFields<T> = {
  [K in keyof T]: T[K] extends Array<infer U>
    ? Array<NumericFields<U>>
    : T[K] extends object
      ? NumericFields<T[K]>
      : Num<T[K]>
}

export type RosterEvent = NumericFields<Schemas["RosterEventResponse"]>

export type RosterSlot = NumericFields<Schemas["RosterSlotResponse"]>

export type SignupInfo = Schemas["SignupResponse"]

export type InviteLink = Schemas["InviteLinkResponse"]

export type CreateEventRequest = NumericFields<Schemas["CreateEventRequest"]>

export type CreateSlotRequest = NumericFields<Schemas["CreateSlotRequest"]>

export type PublicInviteData = NumericFields<Schemas["InvitePageResponse"]>

export type PublicEvent = NumericFields<Schemas["EventPublicResponse"]>

export type PublicSlot = NumericFields<Schemas["SlotAvailabilityResponse"]>

export type SignupManageData = NumericFields<Schemas["SignupManageResponse"]>

export type UpdateSlotRequest = NumericFields<Schemas["UpdateSlotRequest"]>

export type TimeSlotResponse = NumericFields<Schemas["TimeSlotResponse"]>

export type UserMeResponse = NumericFields<Schemas["UserMeResponse"]>
