import { ChevronLeft, ChevronRight } from 'lucide-react';

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPage: (p: number) => void;
}

export default function Pagination({
  page,
  totalPages,
  totalCount,
  pageSize,
  onPage,
}: PaginationProps) {
  if (totalPages <= 1) return null;

  const from = (page - 1) * pageSize + 1;
  const to   = Math.min(page * pageSize, totalCount);

  return (
    <div className="flex items-center justify-between border-t border-gray-200 bg-white px-4 py-3">
      <p className="text-sm text-gray-500">
        Showing <span className="font-medium">{from}</span>–
        <span className="font-medium">{to}</span> of{' '}
        <span className="font-medium">{totalCount}</span>
      </p>
      <div className="flex gap-1">
        <button
          onClick={() => onPage(page - 1)}
          disabled={page <= 1}
          className="btn btn-secondary btn-sm"
        >
          <ChevronLeft size={14} />
          Prev
        </button>
        {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
          const p = totalPages <= 7
            ? i + 1
            : page <= 4
              ? i + 1
              : page >= totalPages - 3
                ? totalPages - 6 + i
                : page - 3 + i;
          return (
            <button
              key={p}
              onClick={() => onPage(p)}
              className={`btn btn-sm w-8 justify-center ${
                p === page
                  ? 'bg-brand-primary text-white border-brand-primary hover:bg-brand-dark'
                  : 'btn-secondary'
              }`}
            >
              {p}
            </button>
          );
        })}
        <button
          onClick={() => onPage(page + 1)}
          disabled={page >= totalPages}
          className="btn btn-secondary btn-sm"
        >
          Next
          <ChevronRight size={14} />
        </button>
      </div>
    </div>
  );
}
