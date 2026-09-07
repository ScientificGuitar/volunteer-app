import { cn } from "@/lib/utils"

interface CapacityBarProps {
  filled: number
  capacity: number
  className?: string
}

export function CapacityBar({ filled, capacity, className }: CapacityBarProps) {
  const percent = capacity > 0 ? Math.min(100, (filled / capacity) * 100) : 0
  const isFull = capacity > 0 && filled >= capacity

  return (
    <div
      className={cn(
        "h-2 w-full overflow-hidden rounded-full bg-muted",
        className
      )}
      role="progressbar"
      aria-valuenow={filled}
      aria-valuemin={0}
      aria-valuemax={capacity}
    >
      <div
        className={cn(
          "h-full transition-all",
          isFull ? "bg-red-500" : "bg-primary"
        )}
        style={{ width: `${percent}%` }}
      />
    </div>
  )
}
