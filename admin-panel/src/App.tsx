import { Routes, Route } from 'react-router-dom';

import Layout from './components/Layout';

import ProtectedRoute from './auth/ProtectedRoute';

import { Permissions } from './auth/permissions';

import Login from './pages/Login';

import AccessDenied from './pages/AccessDenied';

import Dashboard from './pages/Dashboard';

import ProcessDefinitionList from './pages/ProcessDefinitions/List';

import ProcessDefinitionDetail from './pages/ProcessDefinitions/Detail';

import ProcessInstanceList from './pages/ProcessInstances/List';

import ProcessInstanceDetail from './pages/ProcessInstances/Detail';

import JobList from './pages/Jobs/List';

import JobDetail from './pages/Jobs/Detail';

import ArtifactList from './pages/Artifacts/List';

import TriggerList from './pages/Triggers/List';

import TriggerCreate from './pages/Triggers/Create';

import TriggerEvents from './pages/Triggers/Events';

import JobLogs from './pages/Logs/JobLogs';

import WorkflowDesigner from './pages/WorkflowDesigner';

import CredentialList from './pages/Credentials/List';

import WorkerList from './pages/Workers/List';

import WorkerDetail from './pages/Workers/Detail';

import UserList from './pages/Users/List';

import RoleList from './pages/Roles/List';

import AuditList from './pages/Audit/List';

import AuditDetail from './pages/Audit/Detail';



export default function App() {

  return (

    <Routes>

      <Route path="/login" element={<Login />} />

      <Route path="/403" element={<AccessDenied />} />



      <Route element={<ProtectedRoute />}>

        <Route element={<Layout />}>

          <Route element={<ProtectedRoute permission={Permissions.DashboardView} />}>
            <Route index element={<Dashboard />} />
          </Route>



          <Route element={<ProtectedRoute permission={Permissions.WorkflowView} />}>

            <Route path="process-definitions" element={<ProcessDefinitionList />} />

            <Route path="process-definitions/:id" element={<ProcessDefinitionDetail />} />

            <Route path="process-instances" element={<ProcessInstanceList />} />

            <Route path="process-instances/:id" element={<ProcessInstanceDetail />} />

            <Route path="artifacts" element={<ArtifactList />} />

            <Route path="triggers" element={<TriggerList />} />

            <Route path="triggers/:id/events" element={<TriggerEvents />} />

            <Route path="workflow-designer" element={<WorkflowDesigner />} />

          </Route>



          <Route element={<ProtectedRoute permission={Permissions.WorkflowEdit} />}>

            <Route path="triggers/create" element={<TriggerCreate />} />

          </Route>



          <Route element={<ProtectedRoute permission={Permissions.JobView} />}>

            <Route path="jobs" element={<JobList />} />

            <Route path="jobs/:id" element={<JobDetail />} />

            <Route path="logs" element={<JobLogs />} />

          </Route>



          <Route element={<ProtectedRoute permission={Permissions.CredentialManage} />}>

            <Route path="credentials" element={<CredentialList />} />

          </Route>



          <Route element={<ProtectedRoute permission={Permissions.WorkerView} />}>

            <Route path="workers" element={<WorkerList />} />

            <Route path="workers/:workerId" element={<WorkerDetail />} />

          </Route>



          <Route element={<ProtectedRoute adminOnly />}>

            <Route path="users" element={<UserList />} />

            <Route path="roles" element={<RoleList />} />

          </Route>



          <Route element={<ProtectedRoute permission={Permissions.AuditView} />}>

            <Route path="audit" element={<AuditList />} />

            <Route path="audit/:id" element={<AuditDetail />} />

          </Route>

        </Route>

      </Route>

    </Routes>

  );

}


