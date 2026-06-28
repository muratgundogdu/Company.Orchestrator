import { Navigate, Outlet, useLocation } from 'react-router-dom';
import LoadingSpinner from '../components/LoadingSpinner';
import { useAuth } from './AuthContext';
import { Permissions, type Permission } from './permissions';

interface ProtectedRouteProps {
  permission?: Permission | Permission[];
  adminOnly?: boolean;
}

export default function ProtectedRoute({ permission, adminOnly }: ProtectedRouteProps) {
  const { user, loading, hasPermission } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <LoadingSpinner />
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  const required = adminOnly ? Permissions.AdminManage : permission;
  if (required && !hasPermission(required)) {
    return <Navigate to="/403" replace />;
  }

  return <Outlet />;
}
