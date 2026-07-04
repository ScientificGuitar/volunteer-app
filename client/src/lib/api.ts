import type {
  OrganizationDetail,
  RosterEvent,
  CreateEventRequest,
  CreateInviteLinkResponse,
} from "@/lib/types"

const BASE = "/api"

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function authHeaders(getToken: () => Promise<string | null>) {
  const token = await getToken()
  return {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  }
}

async function checkJson<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(res.status, body.error ?? res.statusText)
  }
  return res.json()
}

async function checkVoid(res: Response): Promise<void> {
  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(res.status, body.error ?? res.statusText)
  }
}

export function createAdminApi(getToken: () => Promise<string | null>) {
  const h = () => authHeaders(getToken)

  return {
    getOrganization: async (id: string) => {
      const res = await fetch(`${BASE}/organizations/${id}`, { headers: await h() })
      return checkJson<OrganizationDetail>(res)
    },

    createOrganization: async (name: string) => {
      const res = await fetch(`${BASE}/organizations`, {
        method: "POST",
        headers: await h(),
        body: JSON.stringify({ name }),
      })
      return checkJson<{ id: string; name: string; createdAt: string }>(res)
    },

    createEvent: async (orgId: string, data: CreateEventRequest) => {
      const res = await fetch(`${BASE}/organizations/${orgId}/events`, {
        method: "POST",
        headers: await h(),
        body: JSON.stringify(data),
      })
      return checkJson<{ id: string }>(res)
    },

    updateEvent: async (eventId: string, data: { title?: string; description?: string | null; date?: string }) => {
      const res = await fetch(`${BASE}/events/${eventId}`, {
        method: "PUT",
        headers: await h(),
        body: JSON.stringify(data),
      })
      return checkJson<{ id: string }>(res)
    },

    getEvent: async (id: string) => {
      const res = await fetch(`${BASE}/events/${id}`, { headers: await h() })
      return checkJson<RosterEvent>(res)
    },

    deleteEvent: async (eventId: string) => {
      const res = await fetch(`${BASE}/events/${eventId}`, {
        method: "DELETE",
        headers: await h(),
      })
      await checkVoid(res)
    },

    getRoster: async (orgId: string, weekStart: string) => {
      const res = await fetch(`${BASE}/organizations/${orgId}/roster?weekStart=${weekStart}`, {
        headers: await h(),
      })
      return checkJson<RosterEvent[]>(res)
    },

    deleteSignup: async (signupId: string) => {
      const res = await fetch(`${BASE}/signups/${signupId}`, {
        method: "DELETE",
        headers: await h(),
      })
      await checkVoid(res)
    },

    createInviteLink: async (orgId: string) => {
      const res = await fetch(`${BASE}/organizations/${orgId}/invite-links`, {
        method: "POST",
        headers: await h(),
        body: JSON.stringify({}),
      })
      return checkJson<CreateInviteLinkResponse>(res)
    },
  }
}

export type AdminApi = ReturnType<typeof createAdminApi>
