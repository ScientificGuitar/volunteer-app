import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"
import type { SignupInfo } from "@/lib/types"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function activeSignupCount(signups: SignupInfo[]): number {
  return signups.filter((s) => s.status !== "Cancelled").length
}

export function formatTime(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleTimeString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  })
}

export function toTimeInputValue(iso: string): string {
  return new Date(iso).toLocaleTimeString("sv-SE", {
    hour: "2-digit",
    minute: "2-digit",
  })
}
