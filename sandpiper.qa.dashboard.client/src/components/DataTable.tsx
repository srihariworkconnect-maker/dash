import { useState, type ReactNode } from 'react';

export interface Column { key: string; label: string; render?: (row: DataRow) => ReactNode }
export type DataRow = Record<string, unknown>;
export const cellText = (value: unknown): string => value == null ? 'Unavailable' : String(value);

export default function DataTable({ rows, columns, name, onSelect }: {
    rows: DataRow[]; columns: Column[]; name: string; onSelect?: (row: DataRow) => void;
}) {
    const [search, setSearch] = useState('');
    const [sort, setSort] = useState({ key: columns[0]?.key ?? '', asc: true });
    const [visible, setVisible] = useState(columns.slice(0, 8).map(c => c.key));
    const [filterColumn, setFilterColumn] = useState('');
    const [filterValue, setFilterValue] = useState('');
    const [page, setPage] = useState(0);
    const filtered = rows.filter(row => (!search || columns.some(c => cellText(row[c.key]).toLowerCase().includes(search.toLowerCase()))) &&
        (!filterColumn || !filterValue || cellText(row[filterColumn]).toLowerCase().includes(filterValue.toLowerCase())))
        .sort((a, b) => (typeof a[sort.key] === 'number' && typeof b[sort.key] === 'number'
            ? Number(a[sort.key]) - Number(b[sort.key]) : cellText(a[sort.key]).localeCompare(cellText(b[sort.key]))) * (sort.asc ? 1 : -1));
    const pageCount = Math.max(1, Math.ceil(filtered.length / 50));
    const safePage = Math.min(page, pageCount - 1);
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
        <span>{filtered.length} records</span></div>
        <div className="table-scroll"><table><thead><tr>{columns.filter(c => visible.includes(c.key)).map(c => <th key={c.key}
            aria-sort={sort.key === c.key ? sort.asc ? 'ascending' : 'descending' : 'none'}><button onClick={() => setSort({ key: c.key, asc: sort.key === c.key ? !sort.asc : true })}>{c.label}</button></th>)}</tr></thead>
            <tbody>{filtered.slice(safePage * 50, safePage * 50 + 50).map((row, i) => <tr key={String(row.key ?? i)}>
                {columns.filter(c => visible.includes(c.key)).map((c, j) => <td key={c.key}>{j === 0 && onSelect
                    ? <button className="text-button" onClick={() => onSelect(row)}>{cellText(row[c.key])}</button>
                    : c.render ? c.render(row) : cellText(row[c.key])}</td>)}</tr>)}</tbody></table>
            {!filtered.length && <p className="empty-state">No matching records in this collected scope.</p>}</div>
        <div className="table-tools"><button disabled={safePage === 0} onClick={() => setPage(safePage - 1)}>Previous</button>
            <span>Page {safePage + 1} of {pageCount}</span><button disabled={safePage + 1 >= pageCount} onClick={() => setPage(safePage + 1)}>Next</button></div>
    </div>;
}
