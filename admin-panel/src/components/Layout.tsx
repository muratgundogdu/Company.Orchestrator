import { useState } from 'react';

import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';

import {

  LayoutDashboard,

  FileText,

  Play,

  Briefcase,

  Archive,

  Zap,

  ScrollText,

  ChevronLeft,

  ChevronRight,

  Settings,

  GitBranch,

  KeyRound,

  Server,

  Users,

  Shield,

  ClipboardList,

  LogOut,

} from 'lucide-react';

import { useAuth } from '../auth/AuthContext';

import { Permissions, type Permission } from '../auth/permissions';



interface NavItem {

  to: string;

  icon: React.ReactNode;

  label: string;

  permission?: Permission | Permission[];

}



const NAV: NavItem[] = [

  { to: '/',                    icon: <LayoutDashboard size={18} />, label: 'Dashboard' },

  { to: '/process-definitions', icon: <FileText size={18} />,        label: 'Definitions', permission: Permissions.WorkflowView },

  { to: '/process-instances',   icon: <Play size={18} />,            label: 'Instances', permission: Permissions.WorkflowView },

  { to: '/jobs',                icon: <Briefcase size={18} />,       label: 'Jobs', permission: Permissions.JobView },

  { to: '/artifacts',           icon: <Archive size={18} />,         label: 'Artifacts', permission: Permissions.WorkflowView },

  { to: '/triggers',            icon: <Zap size={18} />,             label: 'Triggers', permission: Permissions.WorkflowView },

  { to: '/credentials',         icon: <KeyRound size={18} />,        label: 'Credentials', permission: Permissions.CredentialManage },

  { to: '/workers',             icon: <Server size={18} />,          label: 'Workers', permission: Permissions.WorkerView },

  { to: '/logs',                icon: <ScrollText size={18} />,      label: 'Logs', permission: Permissions.JobView },

  { to: '/workflow-designer',   icon: <GitBranch size={18} />,       label: 'Designer', permission: Permissions.WorkflowView },

  { to: '/users',               icon: <Users size={18} />,           label: 'Users', permission: Permissions.AdminManage },

  { to: '/roles',               icon: <Shield size={18} />,          label: 'Roles', permission: Permissions.AdminManage },

  { to: '/audit',               icon: <ClipboardList size={18} />,   label: 'Audit Center', permission: Permissions.AuditView },

];



export default function Layout() {

  const [collapsed, setCollapsed] = useState(false);

  const location = useLocation();

  const navigate = useNavigate();

  const { user, logout, hasPermission } = useAuth();



  const visibleNav = NAV.filter(

    (item) => !item.permission || hasPermission(item.permission),

  );



  function handleLogout() {

    logout();

    navigate('/login');

  }



  return (

    <div className="flex h-screen overflow-hidden bg-gray-50">

      <aside

        className={`flex flex-col bg-brand-primary text-white transition-all duration-200 shrink-0 ${

          collapsed ? 'w-14' : 'w-44'

        }`}

      >

        <div className={`flex items-center border-b border-white/15 ${collapsed ? 'justify-center px-2 py-4' : 'gap-2.5 px-3 py-4'}`}>

          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-white shrink-0">

            <Settings size={15} className="text-brand-primary" />

          </div>

          {!collapsed && (

            <span className="font-semibold text-[15px] tracking-tight text-white leading-none">

              AlterOne

            </span>

          )}

        </div>



        <nav className="flex-1 py-3 space-y-0.5 overflow-y-auto">

          {visibleNav.map((item) => {

            const isActive =

              item.to === '/'

                ? location.pathname === '/'

                : location.pathname.startsWith(item.to);



            return (

              <NavLink

                key={item.to}

                to={item.to}

                title={collapsed ? item.label : undefined}

                className={`relative flex items-center rounded-md text-[13px] font-medium transition-colors ${

                  collapsed ? 'justify-center mx-1.5 px-0 py-2.5' : 'gap-2.5 mx-2 px-2.5 py-2'

                } ${

                  isActive

                    ? 'bg-white text-brand-primary shadow-sm font-semibold'

                    : 'text-white/80 hover:bg-white/12 hover:text-white'

                }`}

              >

                {isActive && !collapsed && (

                  <span className="absolute left-0 top-1.5 bottom-1.5 w-0.5 rounded-r bg-brand-primary" aria-hidden />

                )}

                <span className="shrink-0">{item.icon}</span>

                {!collapsed && <span className="truncate">{item.label}</span>}

              </NavLink>

            );

          })}

        </nav>



        <button

          onClick={() => setCollapsed(!collapsed)}

          className="flex items-center justify-center py-2.5 border-t border-white/15 text-white/55 hover:text-white transition-colors"

          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}

        >

          {collapsed ? <ChevronRight size={15} /> : <ChevronLeft size={15} />}

        </button>

      </aside>



      <div className="flex flex-col flex-1 overflow-hidden min-w-0">

        <header className="flex items-center justify-between bg-white border-b border-gray-200 px-5 py-2.5 shrink-0">

          <div className="text-xs text-content/60 font-mono truncate">

            {import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'}

          </div>

          <div className="flex items-center gap-4">

            {user && (

              <span className="text-xs text-content/70">

                {user.displayName || user.username}

                {user.roles.length > 0 && (

                  <span className="text-content/50 ml-1">({user.roles.join(', ')})</span>

                )}

              </span>

            )}

            <button onClick={handleLogout} className="btn btn-ghost btn-sm text-content/70" title="Sign out">

              <LogOut size={14} />

            </button>

          </div>

        </header>



        <main className="flex-1 overflow-y-auto p-5">

          <Outlet />

        </main>

      </div>

    </div>

  );

}


