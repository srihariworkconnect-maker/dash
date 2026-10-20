import { createContext } from 'react';

export interface TableExport { name: string; columns: string[]; rows: (string | null)[][] }
export const TableExportContext = createContext<((table: TableExport, signal: AbortSignal) => Promise<Blob>) | null>(null);
