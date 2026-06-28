import { AlertTriangle, RefreshCw } from 'lucide-react';

interface ErrorAlertProps {
  message: string;
  onRetry?: () => void;
}

export default function ErrorAlert({ message, onRetry }: ErrorAlertProps) {
  return (
    <div className="flex flex-col items-center justify-center py-16 gap-4 text-red-500">
      <AlertTriangle size={36} />
      <p className="text-sm font-medium">{message}</p>
      {onRetry && (
        <button onClick={onRetry} className="btn btn-secondary btn-sm">
          <RefreshCw size={14} />
          Retry
        </button>
      )}
    </div>
  );
}
