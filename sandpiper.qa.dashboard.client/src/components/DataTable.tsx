import { useContext, useMemo, useState, type ReactNode } from 'react';
import { TableExportContext } from './TableExportContext';

export interface Column { key: string; label: string; render?: (row: DataRow) => ReactNode }
export type DataRow = Record<string, unknown>;
const cellText = (value: unknown): string => value == null ? 'Unavailable' : String(value);

export default function DataTable({ rows, columns, name, onSelect }: {
    rows: DataRow[]; columns: Column[]; name: string; onSelect?: (row: DataRow) => void;
}) {
    const [search, setSearch] = useState('');
    const [sort, setSort] = useState({ key: columns[0]?.key ?? '', asc: true });
    const [visible, setVisible] = useState(columns.slice(0, 8).map(c => c.key));
    const [filterColumn, setFilterColumn] = useState('');
    const [filterValue, setFilterValue] = useState('');
    const [page, setPage] = useState(0);
    const [virtual, setVirtual] = useState(false);
    const [scrollTop, setScrollTop] = useState(0);
    const filtered = useMemo(() => rows.filter(row => (!search || columns.some(c => cellText(row[c.key]).toLowerCase().includes(search.toLowerCase()))) &&
        (!filterColumn || !filterValue || cellText(row[filterColumn]).toLowerCase().includes(filterValue.toLowerCase())))
        .sort((a, b) => (typeof a[sort.key] === 'number' && typeof b[sort.key] === 'number'
            ? Number(a[sort.key]) - Number(b[sort.key]) : cellText(a[sort.key]).localeCompare(cellText(b[sort.key]))) * (sort.asc ? 1 : -1)), [rows, columns, search, filterColumn, filterValue, sort]);
    const exporter = useContext(TableExportContext);
    const [exportError, setExportError] = useState('');
    const [exporting, setExporting] = useState(false);
    const exportExcel = async () => {
        if (!exporter) return;
        setExporting(true); setExportError('');
        const selected = columns.filter(c => visible.includes(c.key));
        try {
            const blob = await exporter({ name, columns: selected.map(c => c.label),
                rows: filtered.map(row => selected.map(c => row[c.key] == null ? null : String(row[c.key]))) }, new AbortController().signal);
            const url = URL.createObjectURL(blob), a = document.createElement('a');
            a.href = url; a.download = 'Sandpiper-selected-records.xlsx'; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (e) { setExportError(e instanceof Error ? e.message : 'Workbook export unavailable'); }
        finally { setExporting(false); }
    };
    const exportCsv = () => {
        const selected = columns.filter(c => visible.includes(c.key));
        const quote = (value: unknown) => {
            const text = value == null ? 'Unavailable' : String(value);
            return `"${(/^[\s]*[=+@-]/.test(text) ? `'${text}` : text).replaceAll('"', '""')}"`;
        };
        const csv = [selected.map(c => quote(c.label)).join(','), ...filtered.map(row => selected.map(c => quote(row[c.key])).join(','))].join('\r\n');
        const url = URL.createObjectURL(new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8' }));
        const link = document.createElement('a'); link.href = url; link.download = 'Sandpiper-selected-records.csv'; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    };
    const pageCount = Math.max(1, Math.ceil(filtered.length / 50));
    const safePage = Math.min(page, pageCount - 1);
    const start = virtual ? Math.max(0, Math.floor(scrollTop / 40) - 8) : safePage * 50;
    const end = virtual ? Math.min(filtered.length, start + 32) : start + 50;
    return <div className="data-table"><div className="table-tools">
        <input aria-label={`Search ${name}`} placeholder={`Search ${name}`} value={search} onChange={e => { setSearch(e.target.value); setPage(0); }} />
        <select aria-label="Column filter" value={filterColumn} onChange={e => { setFilterColumn(e.target.value); setPage(0); }}>
            <option value="">All columns</option>{columns.map(c => <option key={c.key} value={c.key}>{c.label}</option>)}</select>
        <input aria-label="Column filter text" value={filterValue} placeholder="Filter selected column" onChange={e => { setFilterValue(e.target.value); setPage(0); }} />
        <details><summary>Columns / saved view</summary><div className="column-picker">{columns.map(c => <label key={c.key}>
            <input type="checkbox" checked={visible.includes(c.key)} onChange={() => setVisible(visible.includes(c.key) ? visible.filter(k => k !== c.key) : [...visible, c.key])} />{c.label}</label>)}
            <button onClick={() => { try { localStorage.setItem(`sandpiper-view:${name}`, JSON.stringify({ visible, sort })); } catch { /* Browser policy can disable storage. */ } }}>Save view</button>
            <button onClick={() => { try { const saved = JSON.parse(localStorage.getItem(`sandpiper-view:${name}`) ?? 'null');
                if (saved && Array.isArray(saved.visible)) setVisible(saved.visible.filter((k: string) => columns.some(c => c.key === k)));
                if (saved?.sort && columns.some(c => c.key === saved.sort.key)) setSort(saved.sort);
            } catch { /* Ignore invalid saved metadata. No report data is persisted. */ } }}>Load view</button></div></details>
        <button onClick={exportCsv}>Export filtered CSV</button>
        {exporter && <button disabled={exporting || !visible.length} onClick={() => { void exportExcel(); }}>{exporting ? 'Exporting…' : 'Export filtered Excel'}</button>}
        <label><input type="checkbox" checked={virtual} onChange={e => { setVirtual(e.target.checked); setScrollTop(0); }} />Virtual scrolling</label>
        <span>{filtered.length} records</span></div>{exportError && <p role="alert">{exportError}</p>}
        <div className={`table-scroll ${virtual ? 'virtual-scroll' : ''}`} onScroll={e => { if (virtual) setScrollTop(e.currentTarget.scrollTop); }}><table><thead><tr>{columns.filter(c => visible.includes(c.key)).map(c => <th key={c.key}
            aria-sort={sort.key === c.key ? sort.asc ? 'ascending' : 'descending' : 'none'}><button onClick={() => setSort({ key: c.key, asc: sort.key === c.key ? !sort.asc : true })}>{c.label}</button></th>)}</tr></thead>
            <tbody>{virtual && start > 0 && <tr aria-hidden="true"><td colSpan={columns.length} style={{ height: start * 40, padding: 0 }} /></tr>}
            {filtered.slice(start, end).map((row, i) => <tr key={String(row.key ?? start + i)} className={virtual ? 'virtual-row' : ''}>
                {columns.filter(c => visible.includes(c.key)).map((c, j) => <td key={c.key}>{j === 0 && onSelect
                    ? <button className="text-button" onClick={() => onSelect(row)}>{cellText(row[c.key])}</button>
                    : c.render ? c.render(row) : cellText(row[c.key])}</td>)}</tr>)}
            {virtual && end < filtered.length && <tr aria-hidden="true"><td colSpan={columns.length} style={{ height: (filtered.length - end) * 40, padding: 0 }} /></tr>}</tbody></table>
            {!filtered.length && <p className="empty-state">No matching records in this collected scope.</p>}</div>
        {!virtual && <div className="table-tools"><button disabled={safePage === 0} onClick={() => setPage(safePage - 1)}>Previous</button>
            <span>Page {safePage + 1} of {pageCount}</span><button disabled={safePage + 1 >= pageCount} onClick={() => setPage(safePage + 1)}>Next</button></div>}
    </div>;
}
