import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"

const timeInputVariants = cva(
  "flex items-center rounded-md border border-input bg-background text-foreground ring-offset-background transition-colors focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50 aria-invalid:border-destructive",
  {
    variants: {
      size: {
        sm: "h-9 px-2.5 text-sm",
        default: "h-9 px-3 py-1 text-base",
        lg: "h-11 px-3 text-base",
      },
    },
    defaultVariants: {
      size: "default",
    },
  }
)

type Period = "AM" | "PM"
type MinuteRoundingMode = "floor" | "ceil" | "nearest"
type SegField = "hours" | "minutes" | "seconds"

interface Segments {
  hours: string
  minutes: string
  seconds: string
  period: Period
}

function parse(raw: string | undefined, mode: "12h" | "24h"): Segments {
  const empty: Segments = { hours: "", minutes: "", seconds: "", period: "AM" }
  if (!raw) return empty

  const [timePart = ""] = raw.split(" ")
  const [h = "", m = "", s = ""] = timePart.split(":")
  const hNum = parseInt(h, 10)
  if (isNaN(hNum)) return empty

  if (mode === "12h") {
    let display: string
    let period: Period
    if (hNum === 0) {
      display = "12"
      period = "AM"
    } else if (hNum < 12) {
      display = String(hNum)
      period = "AM"
    } else if (hNum === 12) {
      display = "12"
      period = "PM"
    } else {
      display = String(hNum - 12)
      period = "PM"
    }
    return { hours: display, minutes: m, seconds: s, period }
  }

  return { hours: h, minutes: m, seconds: s, period: "AM" }
}

function serialize(
  seg: Segments,
  mode: "12h" | "24h",
  withSeconds: boolean
): string {
  if (seg.hours === "" || seg.minutes === "") return ""

  const h = parseInt(seg.hours, 10)
  const m = (seg.minutes || "0").padStart(2, "0")
  const s = (seg.seconds || "0").padStart(2, "0")

  let h24 = h
  if (mode === "12h") {
    if (seg.period === "AM") {
      if (h === 12) h24 = 0
    } else {
      if (h !== 12) h24 = h + 12
    }
  }

  const hStr = String(h24).padStart(2, "0")
  return withSeconds ? `${hStr}:${m}:${s}` : `${hStr}:${m}`
}

function getDigits(raw: string) {
  return raw.normalize("NFKC").replace(/\D/g, "").slice(-2)
}

export interface TimeInputProps
  extends
  Omit<
    React.HTMLAttributes<HTMLDivElement>,
      "onChange" | "defaultValue" | "onBlur"
  >,
  VariantProps<typeof timeInputVariants> {
  /** Controlled value in 24-hour format: "HH:mm" or "HH:mm:ss" */
  value?: string
  /** Uncontrolled initial value in 24-hour format */
  defaultValue?: string
  /** Called with the serialized 24-hour value, or "" while incomplete */
  onChange?: (value: string) => void
  onBlur?: (value: string) => void
  /** Display format. Output value is always 24-hour regardless of this prop. */
  format?: "12h" | "24h"
  /** Show the seconds segment */
  showSeconds?: boolean
  /** Placeholder text shown in each time segment */
  placeholder?: string
  /** Fill empty minutes with "00" when hours are set and focus leaves the control */
  autoFillMinutesOnBlur?: boolean
  /** Round minutes to the nearest interval when focus leaves the control */
  roundMinutesToNearest?: number
  /** How to round minutes when roundMinutesToNearest is set */
  roundMinutesMode?: MinuteRoundingMode
  /** Round the final interval down instead of rolling over past 24:00 */
  roundLastIntervalDown?: boolean
  /** Allow 24-hour values beyond 23:59, such as "27:00" */
  allowOverflowHours?: boolean
  /** Maximum hour allowed when allowOverflowHours is true */
  maxOverflowHours?: number
  /**
   * Allow the mouse wheel to increment or decrement the focused segment.
   * Default false — scroll is suppressed on the inputs to prevent accidental changes
   * and so the page can scroll normally when the cursor drifts over them.
   */
  scrollToStep?: boolean
  /**
   * BCP 47 locale string used to derive locale-appropriate AM/PM labels via
   * Intl.DateTimeFormat. Must be explicit — omitting it keeps the "AM"/"PM"
   * fallback so server and client render identically.
   */
  locale?: string
  /**
   * Override the AM/PM labels directly. Takes precedence over locale.
   * Use this when your i18n library already has the translated strings.
   */
  periodLabels?: { am: string; pm: string }
  disabled?: boolean
  /** Name passed to the hidden input for native form submission */
  name?: string
  minutesAriaLabel?: string
  /** Replace colon separators with unit suffixes, e.g. { hours: "時", minutes: "分", seconds: "秒" } */
  unitSuffixes?: { hours: string; minutes: string; seconds?: string }
}

const TimeInput = React.forwardRef<HTMLInputElement, TimeInputProps>(
  (
    {
      className,
      size,
      value,
      defaultValue,
      onChange,
      format = "24h",
      showSeconds = false,
      placeholder,
      autoFillMinutesOnBlur = false,
      roundMinutesToNearest,
      roundMinutesMode = "nearest",
      roundLastIntervalDown = false,
      allowOverflowHours = false,
      maxOverflowHours = 99,
      scrollToStep = false,
      locale,
      periodLabels,
      disabled = false,
      name,
      id,
      onBlur,
      onPointerDown,
      minutesAriaLabel,
      unitSuffixes,
      ...props
    },
    ref
  ) => {
    const [seg, setSeg] = React.useState<Segments>(() =>
      parse(value ?? defaultValue, format)
    )
    const segRef = React.useRef(seg)
    const lastEmitted = React.useRef(serialize(seg, format, showSeconds))
    const prevFormat = React.useRef(format)

    const [allSelected, setAllSelected] = React.useState(false)
    const allSelectedRef = React.useRef(false)

    function clearAllSelected() {
      allSelectedRef.current = false
      setAllSelected(false)
    }

    function enterAllSelected() {
      allSelectedRef.current = true
      setAllSelected(true)
    }

    const periods = React.useMemo<{ am: string; pm: string }>(() => {
      if (periodLabels) return periodLabels
      if (!locale) return { am: "AM", pm: "PM" }
      try {
        const fmt = new Intl.DateTimeFormat(locale, {
          hour: "numeric",
          hour12: true,
        })
        const am =
          fmt
            .formatToParts(new Date(2000, 0, 1, 6))
            .find((p) => p.type === "dayPeriod")?.value ?? "AM"
        const pm =
          fmt
            .formatToParts(new Date(2000, 0, 1, 18))
            .find((p) => p.type === "dayPeriod")?.value ?? "PM"
        return { am, pm }
      } catch {
        return { am: "AM", pm: "PM" }
      }
    }, [locale, periodLabels])

    React.useEffect(() => {
      const formatChanged = prevFormat.current !== format
      prevFormat.current = format
      if (
        value !== undefined &&
        (value !== lastEmitted.current || formatChanged)
      ) {
        const next = parse(value, format)
        segRef.current = next
        setSeg(next)
      }
    }, [value, format])

    const hoursRef = React.useRef<HTMLInputElement>(null)
    const minutesRef = React.useRef<HTMLInputElement>(null)
    const secondsRef = React.useRef<HTMLInputElement>(null)
    const periodRef = React.useRef<HTMLButtonElement>(null)

    const scrollToStepRef = React.useRef(scrollToStep)
    const stepRef = React.useRef(step)
    scrollToStepRef.current = scrollToStep
    stepRef.current = step

    React.useEffect(() => {
      const inputs: [React.RefObject<HTMLInputElement | null>, SegField][] = [
        [hoursRef, "hours"],
        [minutesRef, "minutes"],
      ]
      if (showSeconds) inputs.push([secondsRef, "seconds"])

      const removals: (() => void)[] = []
      for (const [ref, field] of inputs) {
        const el = ref.current
        if (!el) continue
        const fn = (e: WheelEvent) => {
          e.preventDefault()
          if (scrollToStepRef.current && document.activeElement === el) {
            stepRef.current(field, e.deltaY < 0 ? 1 : -1)
          }
        }
        el.addEventListener("wheel", fn, { passive: false })
        removals.push(() => el.removeEventListener("wheel", fn))
      }
      return () => removals.forEach((fn) => fn())
    }, [showSeconds])

    const overflowHourMax = Number.isFinite(maxOverflowHours)
      ? Math.max(23, Math.min(99, Math.round(maxOverflowHours)))
      : 99
    const hourMax =
      format === "24h" && allowOverflowHours
        ? overflowHourMax
        : format === "12h"
          ? 12
          : 23

    function commit(patch: Partial<Segments>) {
      const next = { ...segRef.current, ...patch }
      segRef.current = next
      setSeg(next)
      const emitted = serialize(next, format, showSeconds)
      lastEmitted.current = emitted
      onChange?.(emitted)
    }

    function isOverflowMaxHour(seg: Segments) {
      if (format !== "24h" || !allowOverflowHours) return false

      const hour = parseInt(seg.hours || "", 10)
      return !isNaN(hour) && hour >= hourMax
    }

    function maxTimeBoundaryPatch(seg: Segments): Partial<Segments> | null {
      if (!isOverflowMaxHour(seg)) return null

      const patch: Partial<Segments> = {}
      if (seg.minutes !== "" && seg.minutes !== "00") patch.minutes = "00"
      if (seg.seconds !== "" && seg.seconds !== "00") patch.seconds = "00"

      return Object.keys(patch).length > 0 ? patch : null
    }

    function handleHours(e: React.ChangeEvent<HTMLInputElement>) {
      let digits = getDigits(e.target.value)
      if (digits.length === 2) {
        digits = String(Math.min(parseInt(digits, 10), hourMax)).padStart(
          2,
          "0"
        )
      }
      const next = { ...segRef.current, hours: digits }
      commit({ hours: digits, ...maxTimeBoundaryPatch(next) })

      const shouldAutoAdvance =
        digits.length === 2 ||
        (format === "12h" && digits.length === 1 && parseInt(digits, 10) > 1) ||
        (format === "24h" &&
          !allowOverflowHours &&
          digits.length === 1 &&
          parseInt(digits, 10) > 2)
      if (shouldAutoAdvance) {
        minutesRef.current?.focus()
        minutesRef.current?.select()
      }
    }

    function startFreshTyping(digit: string) {
      clearAllSelected()
      commit({ hours: digit, minutes: "", seconds: "" })

      const shouldAutoAdvance =
        (format === "12h" && parseInt(digit, 10) > 1) ||
        (format === "24h" && !allowOverflowHours && parseInt(digit, 10) > 2)
      if (shouldAutoAdvance) {
        minutesRef.current?.focus()
        minutesRef.current?.select()
      } else {
        hoursRef.current?.focus()
      }
    }

    function handleGroupKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
      if (disabled) return

      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "a") {
        e.preventDefault()
        enterAllSelected()
        return
      }

      if (allSelectedRef.current) {
        if (/^[0-9]$/.test(e.key)) {
          e.preventDefault()
          startFreshTyping(e.key)
        } else {
          clearAllSelected()
        }
      }
    }

    function handleMinutes(e: React.ChangeEvent<HTMLInputElement>) {
      let digits = getDigits(e.target.value)
      const max = isOverflowMaxHour(segRef.current) ? 0 : 59
      if (digits.length > 0 && max === 0) {
        digits = "00"
      } else if (digits.length === 2) {
        digits = String(Math.min(parseInt(digits, 10), max)).padStart(2, "0")
      }
      commit({ minutes: digits })

      const advance =
        digits.length === 2 || (digits.length === 1 && parseInt(digits, 10) > 5)
      if (advance) {
        if (showSeconds) {
          secondsRef.current?.focus()
          secondsRef.current?.select()
        } else if (format === "12h") {
          periodRef.current?.focus()
        }
      }
    }

    function handleSeconds(e: React.ChangeEvent<HTMLInputElement>) {
      let digits = getDigits(e.target.value)
      const max = isOverflowMaxHour(segRef.current) ? 0 : 59
      if (digits.length > 0 && max === 0) {
        digits = "00"
      } else if (digits.length === 2) {
        digits = String(Math.min(parseInt(digits, 10), max)).padStart(2, "0")
      }
      commit({ seconds: digits })

      const advance =
        digits.length === 2 || (digits.length === 1 && parseInt(digits, 10) > 5)
      if (advance && format === "12h") {
        periodRef.current?.focus()
      }
    }

    function pad(field: "hours" | "minutes" | "seconds") {
      const val = segRef.current[field]
      if (!val) return
      const n = parseInt(val, 10)
      if (isNaN(n)) return

      let clamped: number
      if (field === "hours") {
        const min = format === "12h" ? 1 : 0
        clamped = Math.max(min, Math.min(hourMax, n))
      } else {
        const max = isOverflowMaxHour(segRef.current) ? 0 : 59
        clamped = Math.min(max, Math.max(0, n))
      }
      commit({ [field]: String(clamped).padStart(2, "0") })
    }

    function step(field: "hours" | "minutes" | "seconds", dir: 1 | -1) {
      const val = parseInt(segRef.current[field] || "0", 10)
      let next: number
      if (field === "hours") {
        const min = format === "12h" ? 1 : 0
        next =
          ((val - min + dir + (hourMax - min + 1)) % (hourMax - min + 1)) + min
      } else {
        next = isOverflowMaxHour(segRef.current) ? 0 : (val + dir + 60) % 60
      }
      commit({ [field]: String(next).padStart(2, "0") })
    }

    function handleKeyDown(
      e: React.KeyboardEvent<HTMLInputElement>,
      field: "hours" | "minutes" | "seconds"
    ) {
      if (e.key === "ArrowUp") {
        e.preventDefault()
        step(field, 1)
      }
      if (e.key === "ArrowDown") {
        e.preventDefault()
        step(field, -1)
      }
      if (e.key === "Backspace" && !segRef.current[field]) {
        if (field === "minutes") hoursRef.current?.focus()
        if (field === "seconds") minutesRef.current?.focus()
      }
    }

    function togglePeriod() {
      commit({ period: segRef.current.period === "AM" ? "PM" : "AM" })
    }

    function getMinuteRoundingInterval() {
      if (roundMinutesToNearest === undefined) return null
      if (!Number.isFinite(roundMinutesToNearest)) return null

      const interval = Math.round(roundMinutesToNearest)
      if (interval <= 1) return null

      return Math.min(interval, 60)
    }

    function incrementHour(seg: Segments): Partial<Segments> {
      const hour = parseInt(seg.hours || "0", 10)
      if (format === "24h") {
        if (allowOverflowHours && hour < hourMax) {
          return { hours: String(hour + 1).padStart(2, "0") }
        }

        return { hours: String((hour + 1) % 24).padStart(2, "0") }
      }

      if (hour === 11) {
        return {
          hours: "12",
          period: seg.period === "AM" ? "PM" : "AM",
        }
      }

      return { hours: String(hour === 12 ? 1 : hour + 1).padStart(2, "0") }
    }

    function isLastHourOfDay(seg: Segments) {
      const hour = parseInt(seg.hours || "0", 10)
      if (isNaN(hour)) return false

      return format === "24h" ? hour === 23 : hour === 11 && seg.period === "PM"
    }

    function roundMinutesPatch(seg: Segments): Partial<Segments> | null {
      const interval = getMinuteRoundingInterval()
      if (interval === null || !seg.hours || !seg.minutes) return null

      const minutes = parseInt(seg.minutes, 10)
      if (isNaN(minutes)) return null

      const quotient = minutes / interval
      const rounded =
        roundMinutesMode === "floor"
          ? Math.floor(quotient) * interval
          : roundMinutesMode === "ceil"
            ? Math.ceil(quotient) * interval
            : Math.round(quotient) * interval
      if (rounded >= 60) {
        if (format === "24h" && allowOverflowHours) {
          if (parseInt(seg.hours, 10) >= hourMax) {
            const floored = Math.floor(quotient) * interval
            const flooredMinutes = String(Math.min(floored, 59)).padStart(
              2,
              "0"
            )
            if (flooredMinutes === seg.minutes) return null

            return { minutes: flooredMinutes }
          }

          return {
            ...incrementHour(seg),
            minutes: "00",
          }
        }

        if (roundLastIntervalDown && isLastHourOfDay(seg)) {
          const floored = Math.floor(quotient) * interval
          const flooredMinutes = String(Math.min(floored, 59)).padStart(2, "0")
          if (flooredMinutes === seg.minutes) return null

          return { minutes: flooredMinutes }
        }

        return {
          ...incrementHour(seg),
          minutes: "00",
        }
      }

      const nextMinutes = String(rounded).padStart(2, "0")
      if (nextMinutes === seg.minutes) return null

      return { minutes: nextMinutes }
    }

    function handleBlur(e: React.FocusEvent<HTMLDivElement>) {
      if (disabled || e.currentTarget.contains(e.relatedTarget)) {
        onBlur?.(lastEmitted.current)
        return
      }

      const current = segRef.current
      const next = {
        ...current,
        ...(autoFillMinutesOnBlur && current.hours && current.minutes === ""
          ? { minutes: "00" }
          : {}),
      }
      Object.assign(next, maxTimeBoundaryPatch(next))
      const roundedPatch = roundMinutesPatch(next)
      const patch = {
        ...(next.minutes !== current.minutes ? { minutes: next.minutes } : {}),
        ...roundedPatch,
      }

      if (Object.keys(patch).length > 0) {
        commit(patch)
      }
      clearAllSelected()
      onBlur?.(lastEmitted.current)
    }

    function handlePointerDown(e: React.PointerEvent<HTMLDivElement>) {
      onPointerDown?.(e)
      if (disabled || e.defaultPrevented) return

      clearAllSelected()

      const target = e.target as HTMLElement
      const startField = target
        .closest("[data-field]")
        ?.getAttribute("data-field") as SegField | null
      const touchedFields = new Set<SegField>(startField ? [startField] : [])

      function onMove(ev: PointerEvent) {
        const el = document.elementFromPoint(ev.clientX, ev.clientY)
        const field = el
          ?.closest("[data-field]")
          ?.getAttribute("data-field") as SegField | undefined
        if (field) touchedFields.add(field)
        if (touchedFields.size > 1 && !allSelectedRef.current) {
          enterAllSelected()
        }
      }
      function onUp() {
        window.removeEventListener("pointermove", onMove)
        window.removeEventListener("pointerup", onUp)
        window.removeEventListener("pointercancel", onUp)
      }
      window.addEventListener("pointermove", onMove)
      window.addEventListener("pointerup", onUp)
      window.addEventListener("pointercancel", onUp)

      if (target.closest("input, button")) return

      e.preventDefault()
      hoursRef.current?.focus()
    }

    const segCls =
      "w-6 bg-transparent text-center tabular-nums outline-none disabled:cursor-not-allowed [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none selection:bg-ring/25 selection:text-foreground"
    const sepCls = "select-none text-foreground"
    const highlightCls = "bg-ring/25"
    const inputHighlightCls = cn(
      highlightCls,
      "caret-transparent selection:bg-transparent"
    )

    return (
      <div
        role="group"
        aria-label="Time input"
        onBlur={handleBlur}
        onPointerDown={handlePointerDown}
        onKeyDown={handleGroupKeyDown}
        className={cn(
          timeInputVariants({ size }),
          !disabled && "cursor-text",
          disabled && "cursor-not-allowed opacity-50",
          className
        )}
        {...props}
      >
        <input
          ref={ref}
          type="hidden"
          name={name}
          value={serialize(seg, format, showSeconds)}
          disabled={disabled}
        />
        <input
          id={id}
          ref={hoursRef}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          placeholder={placeholder ?? (format === "12h" ? "12" : "00")}
          min={format === "12h" ? 1 : 0}
          max={hourMax}
          disabled={disabled}
          value={seg.hours}
          onChange={handleHours}
          onBlur={() => {
            pad("hours")
            onBlur?.(lastEmitted.current)
          }}
          onKeyDown={(e) => handleKeyDown(e, "hours")}
          onFocus={(e) => e.target.select()}
          className={cn(segCls, allSelected && inputHighlightCls)}
          aria-label="Hours"
          data-field="hours"
        />
        <span
          className={cn(sepCls, allSelected && highlightCls)}
          aria-hidden="true"
        >
          {unitSuffixes?.hours ?? ":"}
        </span>
        <input
          ref={minutesRef}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          placeholder={placeholder ?? "00"}
          min={0}
          max={59}
          step={roundMinutesToNearest ?? 1}
          disabled={disabled}
          value={seg.minutes}
          onChange={handleMinutes}
          onBlur={() => {
            pad("minutes")
            onBlur?.(lastEmitted.current)
          }}
          onKeyDown={(e) => handleKeyDown(e, "minutes")}
          onFocus={(e) => e.target.select()}
          className={cn(segCls, allSelected && inputHighlightCls)}
          aria-label={minutesAriaLabel}
          data-field="minutes"
        />
        {unitSuffixes && (
          <span
            className={cn(sepCls, allSelected && highlightCls)}
            aria-hidden="true"
          >
            {unitSuffixes.minutes}
          </span>
        )}
        {showSeconds && (
          <>
            {!unitSuffixes && (
              <span
                className={cn(sepCls, allSelected && highlightCls)}
                aria-hidden="true"
              >
                :
              </span>
            )}
            <input
              ref={secondsRef}
              type="text"
              inputMode="numeric"
              autoComplete="off"
              placeholder={placeholder ?? "00"}
              min={0}
              max={59}
              step={roundMinutesToNearest ?? 1}
              disabled={disabled}
              value={seg.seconds}
              onChange={handleSeconds}
              onBlur={() => {
                pad("seconds")
                onBlur?.(lastEmitted.current)
              }}
              onKeyDown={(e) => handleKeyDown(e, "seconds")}
              onFocus={(e) => e.target.select()}
              className={cn(segCls, allSelected && inputHighlightCls)}
              aria-label="Seconds"
              data-field="seconds"
            />
            {unitSuffixes?.seconds && (
              <span
                className={cn(sepCls, allSelected && highlightCls)}
                aria-hidden="true"
              >
                {unitSuffixes.seconds}
              </span>
            )}
          </>
        )}
        {format === "12h" && (
          <button
            ref={periodRef}
            type="button"
            disabled={disabled}
            onClick={togglePeriod}
            onKeyDown={(e) => {
              if (
                e.key === "ArrowUp" ||
                e.key === "ArrowDown" ||
                e.key === " "
              ) {
                e.preventDefault()
                togglePeriod()
              }
            }}
            className={cn(
              "ml-1.5 min-w-[2.25rem] rounded px-1 py-0.5 text-xs font-semibold text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground focus:bg-accent focus:text-accent-foreground focus:outline-none disabled:pointer-events-none",
              allSelected && highlightCls
            )}
            aria-label={`Toggle period, currently ${seg.period === "AM" ? periods.am : periods.pm}`}
          >
            {seg.period === "AM" ? periods.am : periods.pm}
          </button>
        )}
      </div>
    )
  }
)

TimeInput.displayName = "TimeInput"

export { TimeInput, timeInputVariants }
