import { Link } from 'react-router-dom';
import { ShieldX } from 'lucide-react';
import PageHeader from '../components/PageHeader';

export default function AccessDenied() {
  return (
    <div className="max-w-lg mx-auto mt-16 text-center">
      <div className="inline-flex h-16 w-16 items-center justify-center rounded-full bg-red-50 text-red-600 mb-4">
        <ShieldX size={32} />
      </div>
      <PageHeader title="403 — Access Denied" subtitle="You do not have permission to view this page." />
      <Link to="/" className="btn btn-primary mt-4 inline-flex">
        Back to Dashboard
      </Link>
    </div>
  );
}
