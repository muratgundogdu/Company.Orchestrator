import { useEffect, useState } from 'react';

interface CancelJobModalProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
  loading?: boolean;
}

export default function CancelJobModal({ open, onClose, onConfirm, loading }: CancelJobModalProps) {
  const [reason, setReason] = useState('');

  useEffect(() => {
    if (!open) setReason('');
  }, [open]);

  if (!open) return null;

  function handleConfirm() {
    onConfirm(reason.trim());
  }

  function handleClose() {
    if (loading) return;
    setReason('');
    onClose();
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 overflow-hidden">
        <div className="px-5 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Cancel Job</h2>
          <p className="text-sm text-gray-500 mt-1">
            Are you sure you want to cancel this job?
          </p>
        </div>

        <div className="px-5 py-4">
          <label className="label">Reason (optional)</label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
            className="input text-sm resize-y"
            placeholder="User requested cancellation"
            disabled={loading}
          />
        </div>

        <div className="px-5 py-4 border-t border-gray-100 flex justify-end gap-2 bg-gray-50">
          <button onClick={handleClose} className="btn btn-secondary" disabled={loading}>
            Keep Running
          </button>
          <button onClick={handleConfirm} className="btn btn-danger" disabled={loading}>
            {loading ? 'Cancelling…' : 'Cancel Job'}
          </button>
        </div>
      </div>
    </div>
  );
}
