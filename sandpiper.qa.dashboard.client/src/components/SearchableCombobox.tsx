import { useEffect, useId, useRef, useState } from 'react';

export interface ComboOption { value: string; label: string; depth?: number }
export default function SearchableCombobox({ label, value, options, onChange, loading = false, error }: {
    label: string; value: string; options: ComboOption[]; onChange: (value: string) => void; loading?: boolean; error?: string | null;
}) {
    const id = useId(), root = useRef<HTMLDivElement>(null), trigger = useRef<HTMLButtonElement>(null), input = useRef<HTMLInputElement>(null);
    const [open, setOpen] = useState(false), [search, setSearch] = useState(''), [active, setActive] = useState(0);
    const filtered = options.filter(o => o.label.toLowerCase().includes(search.trim().toLowerCase()));
    const index = Math.min(active, Math.max(0, filtered.length - 1));
    const close = (restore: boolean) => { setOpen(false); if (restore) trigger.current?.focus(); };
    const select = (next: string) => { onChange(next); close(true); };
    const show = () => { setSearch(''); setActive(Math.max(0, options.findIndex(o => o.value === value))); setOpen(true); };
    useEffect(() => {
        if (!open) return;
        input.current?.focus();
        const outside = (e: PointerEvent) => { if (!root.current?.contains(e.target as Node)) setOpen(false); };
        document.addEventListener('pointerdown', outside);
        return () => document.removeEventListener('pointerdown', outside);
    }, [open]);
    useEffect(() => { if (open) document.getElementById(`${id}-option-${index}`)?.scrollIntoView({ block: 'nearest' }); }, [open, index, id]);
    return <div className="search-combo" ref={root} onBlur={e => { if (!e.currentTarget.contains(e.relatedTarget as Node)) setOpen(false); }}>
        <span id={`${id}-label`} className="filter-label">{label}</span>
        <button type="button" ref={trigger} role="combobox" aria-labelledby={`${id}-label`} aria-expanded={open} aria-controls={`${id}-list`} aria-haspopup="listbox"
            className="combo-trigger" onClick={() => open ? close(false) : show()} onKeyDown={e => { if (['ArrowDown', 'ArrowUp'].includes(e.key)) { e.preventDefault(); show(); } }}>
            <span>{options.find(o => o.value === value)?.label ?? (loading ? 'Loading…' : error ? 'Unavailable' : 'Select…')}</span><span aria-hidden="true">▾</span></button>
        {open && <div className="combo-popup"><input ref={input} role="searchbox" aria-label={`Search ${label}`} placeholder={`Search ${label.toLowerCase()}…`}
            aria-controls={`${id}-list`} aria-activedescendant={filtered.length ? `${id}-option-${index}` : undefined} value={search}
            onChange={e => { setSearch(e.target.value); setActive(0); }} onKeyDown={e => {
                if (e.key === 'Escape') { e.preventDefault(); close(true); }
                if (e.key === 'ArrowDown') { e.preventDefault(); setActive(i => Math.min(filtered.length - 1, i + 1)); }
                if (e.key === 'ArrowUp') { e.preventDefault(); setActive(i => Math.max(0, i - 1)); }
                if (e.key === 'Home') { e.preventDefault(); setActive(0); }
                if (e.key === 'End') { e.preventDefault(); setActive(Math.max(0, filtered.length - 1)); }
                if (e.key === 'Enter' && filtered[index]) { e.preventDefault(); select(filtered[index].value); }
                if (e.key === 'Tab') setOpen(false);
            }} />
            {loading && <p role="status">Loading options…</p>}{error && <p role="alert">{error}</p>}
            <ul role="listbox" id={`${id}-list`} aria-labelledby={`${id}-label`}>{filtered.map((o, i) => <li role="option" id={`${id}-option-${i}`} key={o.value}
                aria-selected={o.value === value} className={i === index ? 'active' : ''} style={{ paddingLeft: 12 + (o.depth ?? 0) * 14 }}
                onPointerMove={() => setActive(i)} onMouseDown={e => e.preventDefault()} onClick={() => select(o.value)}>{o.depth ? '↳ ' : ''}{o.label}</li>)}</ul>
            {!loading && !error && !filtered.length && <p role="status">No matching options</p>}</div>}
    </div>;
}
