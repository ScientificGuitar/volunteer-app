import { formatTime } from "@/lib/utils"
import type { RosterEvent } from "@/lib/types"

export const CSV_DELIMITER = ","

export function escapeCsvField(
  value: string | null | undefined,
  delimiter = CSV_DELIMITER
): string {
  const text = value ?? ""
  const needsQuotes =
    text.includes(delimiter) ||
    text.includes('"') ||
    text.includes("\n") ||
    text.includes("\r")
  const escaped = text.replace(/"/g, '""')
  return needsQuotes ? `"${escaped}"` : escaped
}

const VOLUNTEER_CSV_HEADERS = [
  "Event",
  "Date",
  "Location",
  "Slot",
  "Start",
  "End",
  "Volunteer Name",
  "Email",
  "Status",
  "Signed Up At",
]

export function buildEventVolunteersCsv(
  event: RosterEvent,
  delimiter = CSV_DELIMITER
): string {
  const escape = (v: string | null | undefined) => escapeCsvField(v, delimiter)

  const lines = [VOLUNTEER_CSV_HEADERS.map(escape).join(delimiter)]

  for (const slot of event.slots) {
    for (const s of slot.signups) {
      lines.push(
        [
          event.title,
          event.date,
          event.location ?? "",
          slot.label,
          formatTime(slot.startTime),
          formatTime(slot.endTime),
          s.volunteerName,
          s.email,
          s.status,
          new Date(s.createdAt).toLocaleString(),
        ]
          .map(escape)
          .join(delimiter)
      )
    }
  }

  // CRLF so Excel on Windows parses rows correctly.
  return lines.join("\r\n")
}

export function buildVolunteersFilename(title: string, date: string): string {
  const slug =
    title
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "") || "event"
  return `${slug}-${date}-volunteers.csv`
}

export function downloadCsv(filename: string, csvContent: string): void {
  // UTF-8 BOM so Excel detects encoding (umlauts etc.) correctly.
  const blob = new Blob(["\uFEFF" + csvContent], {
    type: "text/csv;charset=utf-8",
  })
  const url = URL.createObjectURL(blob)
  const link = document.createElement("a")
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}
