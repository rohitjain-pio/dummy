import { useMemo, useState } from 'react';
import { Button } from './index';

// Client-side paging — the list endpoints return raw arrays (no server paging exists in the API).
export function usePaged<T>(rows: T[], pageSize = 10) {
  const [page, setPage] = useState(0);
  const pages = Math.max(1, Math.ceil(rows.length / pageSize));
  const current = Math.min(page, pages - 1);
  const slice = useMemo(() => rows.slice(current * pageSize, (current + 1) * pageSize), [rows, current, pageSize]);
  return { slice, page: current, pages, setPage, total: rows.length, pageSize };
}

export function Pagination({ page, pages, total, pageSize, setPage, noun }: { page: number; pages: number; total: number; pageSize: number; setPage: (p: number) => void; noun: string }) {
  if (total === 0) return null;
  const from = page * pageSize + 1;
  const to = Math.min(total, (page + 1) * pageSize);
  return (
    <nav aria-label={`${noun} pages`} className="mt-4 flex flex-wrap items-center justify-between gap-3 text-body-sm text-text-secondary">
      <p aria-live="polite">Showing {from}–{to} of {total} {noun}</p>
      <div className="flex gap-2">
        <Button size="sm" variant="outline" disabled={page === 0} onClick={() => setPage(page - 1)}>Previous</Button>
        <Button size="sm" variant="outline" disabled={page >= pages - 1} onClick={() => setPage(page + 1)}>Next</Button>
      </div>
    </nav>
  );
}
