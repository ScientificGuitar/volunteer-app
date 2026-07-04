import { useState } from "react"
import { toast } from "sonner"
import { useOrg } from "@/hooks/useOrg"
import { useApi } from "@/hooks/useApi"
import { WeeklyGrid } from "@/components/admin/WeeklyGrid"
import { Button } from "@/components/ui/button"

export function Dashboard() {
  const { org, loading } = useOrg()
  const api = useApi()
  const [showCreateOrg, setShowCreateOrg] = useState(false)
  const [orgName, setOrgName] = useState("")
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (loading) {
    return <div className="py-12 text-center text-muted-foreground">Loading...</div>
  }

  if (!org) {
    if (!showCreateOrg) {
      return (
        <div className="mx-auto max-w-md py-12 text-center">
          <h2 className="mb-2 text-xl font-bold">Welcome to Rosterly</h2>
          <p className="mb-6 text-muted-foreground">
            Create an organization to get started with volunteer scheduling.
          </p>
          <Button onClick={() => setShowCreateOrg(true)}>
            Create Organization
          </Button>
        </div>
      )
    }

    return (
      <div className="mx-auto max-w-md py-12">
        <h2 className="mb-4 text-xl font-bold">Create Organization</h2>
        <form
          onSubmit={async (e) => {
            e.preventDefault()
            setError(null)
            setCreating(true)
            try {
              const result = await api.createOrganization(orgName)
              toast.success(`Organization "${result.name}" created`)
              window.location.reload()
            } catch (e) {
              const msg = e instanceof Error ? e.message : "Something went wrong"
              setError(msg)
              toast.error(msg)
            } finally {
              setCreating(false)
            }
          }}
          className="space-y-4"
        >
          <div>
            <label htmlFor="name" className="mb-1 block text-sm font-medium">
              Organization Name
            </label>
            <input
              id="name"
              type="text"
              value={orgName}
              onChange={(e) => setOrgName(e.target.value)}
              className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              required
              placeholder="My Church"
            />
          </div>
          {error && (
            <p className="text-sm text-destructive">{error}</p>
          )}
          <div className="flex gap-2">
            <Button type="submit" disabled={creating}>
              {creating ? "Creating..." : "Create"}
            </Button>
            <Button type="button" variant="outline" onClick={() => { setShowCreateOrg(false); setError(null) }}>
              Cancel
            </Button>
          </div>
        </form>
      </div>
    )
  }

  return (
    <div>
      <h1 className="mb-6 text-2xl font-bold">{org.name}</h1>
      <WeeklyGrid orgId={org.id} api={api} />
    </div>
  )
}
