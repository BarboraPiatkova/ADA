/** A search field: magnifier, visible placeholder in secondary ink (readable contrast), label for screen readers. */
export function SearchInput({ label, placeholder, value, onChange }: { label: string; placeholder: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="inline-flex h-8 items-center gap-1.5 rounded-lg touch-target border border-rule bg-paper px-2.5 text-ink-2 focus-within:border-route focus-within:shadow-[0_0_0_1px_var(--route)]">
      <span className="sr-only">{label}</span>
      <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-3.5-3.5" />
      </svg>
      <input
        className="w-[160px] bg-transparent text-ink outline-none placeholder:text-ink-2 md:w-[220px]"
        type="search"
        value={value}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  )
}
