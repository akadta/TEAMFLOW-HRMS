import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { Layouts } from './layouts/layouts';
import { Login } from './pages/auth/login/login';
import { Dashboard } from './pages/dashboard/dashboard';
import { EmployeeRegister } from './pages/employees/employee-register/employee-register';
import { EmployeeList } from './pages/employees/employee-list/employee-list';
import { EmployeeEdit } from './pages/employees/employee-edit/employee-edit';
import { CompanyEmployeePermission } from './pages/RBAC/company-employee-permission/company-employee-permission';
import { permissionGuard } from './core/rbac/permission.guard';
import { AttendanceApproval } from './attendance/attendance-approval/attendance-approval';
import { AttendanceReport } from './attendance/attendance-report/attendance-report';
import { CompanyRoles } from './pages/company-roles/company-roles';
import { LeaveApprovals } from './leave/leave-approvals/leave-approvals';
import { MyRequests } from './my-requests/my-requests/my-requests';
import { AttendanceCheckins } from './attendance/attendance-checkins/attendance-checkins';
import { MyAttendance } from './my-attendance/my-attendance/my-attendance';
import { AttendanceLogins } from './attendance/attendance-logins/attendance-logins';
import { SelfTasks } from './tasks/self-tasks/self-tasks';
import { SelfTaskForm } from './tasks/self-task-form/self-task-form';
import { DailyTasks } from './tasks/daily-tasks/daily-tasks';
import { DailyTaskForm } from './tasks/daily-task-form/daily-task-form';
import { TeamOverview } from './team/team-overview/team-overview';
import { AssignedTasks } from './tasks/assigned-tasks/assigned-tasks';
import { AssignTasks } from './tasks/assign-tasks/assign-tasks';
import { TaskInbox } from './tasks/task-inbox/task-inbox';
import { TaskApprovals } from './tasks/task-approvals/task-approvals';
import { TaskSettings } from './tasks/task-settings/task-settings';
export const routes: Routes = [
  // login: no guard, always reachable (shows the last company's theme)
  { path: '', pathMatch: 'full', component: Login },
  { path: 'login', component: Login },

  // everything inside the layout needs a valid cookie
  {
    path: '',
    component: Layouts,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: Dashboard }, // no guard: it is the fallback page
      // EMPLOYEE
      {
        path: 'employee/register',
        component: EmployeeRegister,
        canActivate: [permissionGuard],
        data: { permission: 'employees.register' },
      },
      {
        path: 'employee/list',
        component: EmployeeList,
        canActivate: [permissionGuard],
        data: { permission: 'employees.list' },
      },
      {
        path: 'employee/edit/:id',
        component: EmployeeEdit,
        canActivate: [permissionGuard],
        data: { permission: 'employees.edit' },
      },
      // RBAC
      {
        path: 'company/employee/permission',
        component: CompanyEmployeePermission,
        canActivate: [permissionGuard],
        data: { permission: 'role.permission' },
      },
      { path: 'my-attendance', component: MyAttendance },
      {
        path: 'attendance/logins',
        component: AttendanceLogins,
        canActivate: [permissionGuard],
        data: { permission: 'attendance.report' },
      },
      // TASKS
      { path: 'tasks/assigned', component: AssignedTasks },
      { path: 'tasks/assign', component: AssignTasks },
      {
        path: 'tasks/inbox',
        component: TaskInbox,
        canActivate: [permissionGuard],
        data: { ownerOnly: true },
      },
      { path: 'tasks/approvals', component: TaskApprovals },
      {
        path: 'tasks/settings',
        component: TaskSettings,
        canActivate: [permissionGuard],
        data: { ownerOnly: true },
      },
      { path: 'tasks/daily', component: DailyTasks },
      { path: 'tasks/daily/new', component: DailyTaskForm },
      { path: 'tasks/daily/:id', component: DailyTaskForm },
      {
        path: 'team',
        component: TeamOverview,
        canActivate: [permissionGuard],
        data: { permission: 'team.view' },
      },
      { path: 'tasks/self', component: SelfTasks },
      { path: 'tasks/self/new', component: SelfTaskForm },
      { path: 'tasks/self/:id', component: SelfTaskForm },
      // MY REQUESTS (everyone sees only their own)
      { path: 'my-requests', component: MyRequests },
      // ATTENDANCE
      {
        path: 'attendance/approval',
        component: AttendanceApproval,
        canActivate: [permissionGuard],
        data: { permission: 'attendance.approval' },
      },
      {
        path: 'attendance/report',
        component: AttendanceReport,
        canActivate: [permissionGuard],
        data: { permission: 'attendance.report' },
      },
      {
        path: 'attendance/checkins',
        component: AttendanceCheckins,
        canActivate: [permissionGuard],
        data: { permission: 'attendance.report' },
      },
      // LEAVE (no permission slug: shows only the requests where you are an approver)
      {
        path: 'leave/approvals',
        component: LeaveApprovals,
        // canActivate: [permissionGuard],

        // data: { permission: 'leave.approval' },
      },
      //ROLES
      {
        path: 'company/roles',
        component: CompanyRoles,
        canActivate: [permissionGuard],
        data: { permission: 'company.role' },
      },
    ],
  },

  { path: '**', redirectTo: '' },
];
