import { useEffect, useState } from "react"
import { toast } from "sonner"
import { Plus, Copy } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useOrg } from "@/hooks/useOrg"
import { useApi } from "@/hooks/useApi"
import { useOrganization } from "@/hooks/useOrganization"
import type { InviteLinkInfo } from "@/lib/types"

export function InviteLinks() {
  const { org } = useOrg()
  const api = useApi()
  const { data: orgDetail, isLoading } = useOrganization(org?.id)
  const [links, setLinks] = useState<InviteLinkInfo[]>([])

  useEffect(() => {
    setLinks(orgDetail?.inviteLinks ?? [])
  }, [orgDetail])

  const handleCreate = async () => {
    if (!org) return
    try {
      const newLink = await api.createInviteLink(org.id)
      const origin = window.location.origin
      const fullUrl = `${origin}/invite/${newLink.code}`
      setLinks((prev) => [
        ...prev,
        { id: newLink.id, code: newLink.code, isActive: newLink.isActive, createdAt: newLink.createdAt },
      ])
      await navigator.clipboard.writeText(fullUrl)
      toast.success("Invite link copied to clipboard")
    } catch (e) {
      const msg = e instanceof Error ? e.message : "Failed to create invite link"
      toast.error(msg)
    }
  }

  const handleCopy = async (code: string) => {
    const origin = window.location.origin
    const fullUrl = `${origin}/invite/${code}`
    try {
      await navigator.clipboard.writeText(fullUrl)
      toast.success("Link copied")
    } catch {
      const input = document.createElement("input")
      input.value = fullUrl
      document.body.appendChild(input)
      input.select()
      document.execCommand("copy")
      document.body.removeChild(input)
      toast.success("Link copied")
    }
  }

  if (!org) return null

  if (isLoading) {
    return (
      <div className="mx-auto max-w-lg">
        <h1 className="mb-6 text-2xl font-bold">Invite Links</h1>
        <div className="py-12 text-center text-muted-foreground">Loading...</div>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-lg">
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold">Invite Links</h1>
        <Button onClick={handleCreate}>
          <Plus className="mr-1 h-4 w-4" /> Generate Link
        </Button>
      </div>

      {links.length === 0 && (
        <div className="py-12 text-center text-muted-foreground">
          No invite links yet. Generate one to share with volunteers.
        </div>
      )}

      {links.length > 0 && (
        <div className="space-y-3">
          {links.map((link) => {
            const fullUrl = `${window.location.origin}/invite/${link.code}`
            return (
              <Card key={link.id}>
                <CardHeader className="p-4 pb-0">
                  <CardTitle className="flex items-center justify-between text-sm">
                    <span className="font-mono text-xs">{link.code}</span>
                    <Badge
                      variant={link.isActive ? "default" : "secondary"}
                      className="text-[10px]"
                    >
                      {link.isActive ? "Active" : "Inactive"}
                    </Badge>
                  </CardTitle>
                </CardHeader>
                <CardContent className="p-4 pt-2">
                  <div className="flex items-center gap-2">
                    <Input
                      readOnly
                      value={fullUrl}
                      className="h-8 text-xs"
                      onClick={(e) => e.currentTarget.select()}
                    />
                    <Button
                      variant="outline"
                      size="icon"
                      className="h-8 w-8 shrink-0"
                      onClick={() => handleCopy(link.code)}
                      title="Copy to clipboard"
                    >
                      <Copy className="h-3 w-3" />
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )
          })}
        </div>
      )}
    </div>
  )
}
