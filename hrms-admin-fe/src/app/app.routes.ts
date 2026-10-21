import { Routes } from '@angular/router';
import { Login } from './pages/auth/login/login';
import { Layouts } from './layouts/layouts';
import { Dashboard } from './pages/dashboard/dashboard';
import { CompanyRegister } from './pages/companies/company-register/company-register';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/rbac/permission.guard';
import { CompanyList } from './pages/companies/company-list/company-list';
import { CompanyRoles } from './pages/companies/company-roles/company-roles';
import { EmployeeRegister } from './pages/employees/employee-register/employee-register';
import { EmployeeList } from './pages/employees/employee-list/employee-list';
import { EmployeeEdit } from './pages/employees/employee-edit/employee-edit';
import { ReportingManagersHierarchy } from './pages/allocations/reporting-managers-hierarchy/reporting-managers-hierarchy';
import { HierarchyTree } from './pages/allocations/hierarchy-tree/hierarchy-tree';
import { AttendanceSettings } from './pages/attendance/attendance-settings/attendance-settings';
import { AttendanceTiming } from './pages/attendance/attendance-timing/attendance-timing';
import { Roles } from './pages/roles/roles';
import { MainEmployeeForm } from './pages/main-employees/main-employee-form/main-employee-form';
import { MainEmployeeList } from './pages/main-employees/main-employee-list/main-employee-list';
import { MyCompanies } from './pages/my-companies/my-companies/my-companies';
import { CompanyEmployees } from './pages/my-companies/company-employees/company-employees';
import { EmployeeAttendance } from './pages/my-companies/employee-attendance/employee-attendance';
import { MainEmployeePermission } from './pages/rbac/main-employee-permission/main-employee-permission';
import { LeaveSettings } from './pages/leave/leave-settings/leave-settings';
import { LeaveApprovals } from './pages/leave/leave-approvals/leave-approvals';
import { TeamOverview } from './pages/team/team-overview/team-overview';
import { TaskSettings } from './pages/tasks/task-settings/task-settings';
import { AssignTasks } from './pages/tasks/assign-tasks/assign-tasks';
import { TaskApprovals } from './pages/tasks/task-approvals/task-approvals';
export const routes: Routes = [
  { path: '', component: Login, pathMatch: 'full' },
  {
    path: '',
    component: Layouts,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: 'dashboard', component: Dashboard },

      // COMPANY
      {
        path: 'company/register',
        component: CompanyRegister,
        canActivate: [permissionGuard],
        data: { permission: 'company.register' },
      },
      {
        path: 'company/list',
        component: CompanyList,
        canActivate: [permissionGuard],
        data: { permission: 'company.list' },
      },
      {
        path: 'company/roles',
        component: CompanyRoles,
        canActivate: [permissionGuard],
        data: { permission: 'company.roles' },
      },

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

      // ALLOCATIONS
      {
        path: 'allocations/reporting-managers',
        component: ReportingManagersHierarchy,
        canActivate: [permissionGuard],
        data: { permission: 'allocations.reporting-managers' },
      },
      {
        path: 'hierarchy/tree',
        component: HierarchyTree,
        canActivate: [permissionGuard],
        data: { permission: 'hierarchy.tree' },
      },

      // ATTENDANCE
      {
        path: 'attendance/settings',
        component: AttendanceSettings,
        canActivate: [permissionGuard],
        data: { permission: 'attendance.holidays' },
      },
      {
        path: 'attendance/timing',
        component: AttendanceTiming,
        canActivate: [permissionGuard],
        data: { permission: 'attendance.timing' },
      },
      {
        path: 'attendance/leave-settings',
        component: LeaveSettings,
        canActivate: [permissionGuard],
        data: { permission: 'leave.setting' },
      },
      {
        path: 'leave/approvals',
        component: LeaveApprovals,
        canActivate: [permissionGuard],
        data: { permission: 'leave.approval' },
      },
      { path: 'team', component: TeamOverview },
      { path: 'tasks/settings', component: TaskSettings },
      { path: 'tasks/assign', component: AssignTasks },
      { path: 'tasks/approvals', component: TaskApprovals },

      // MAIN TEAM
      {
        path: 'roles',
        component: Roles,
        canActivate: [permissionGuard],
        data: { permission: 'roles.manage' },
      },
      {
        path: 'main-employee/register',
        component: MainEmployeeForm,
        canActivate: [permissionGuard],
        data: { permission: 'main-employees.register' },
      },
      {
        path: 'main-employee/edit/:id',
        component: MainEmployeeForm,
        canActivate: [permissionGuard],
        data: { permission: 'main-employees.edit' },
      },
      {
        path: 'main-employee/list',
        component: MainEmployeeList,
        canActivate: [permissionGuard],
        data: { permission: 'main-employees.list' },
      },

      // MY COMPANY: companies -> employees (view only)
      {
        path: 'my-companies',
        component: MyCompanies,
        canActivate: [permissionGuard],
        data: { base: 'my-companies', title: 'My company', permission: 'my-companies.view' },
      },
      {
        path: 'my-companies/:companyId',
        component: CompanyEmployees,
        canActivate: [permissionGuard],
        data: { base: 'my-companies', permission: 'my-companies.view' },
      },

      // ATTENDANCE: companies -> employees -> attendance report
      {
        path: 'my-attendance',
        component: MyCompanies,
        canActivate: [permissionGuard],
        data: { base: 'my-attendance', title: 'Attendance', permission: 'my-attendance.view' },
      },
      {
        path: 'my-attendance/:companyId',
        component: CompanyEmployees,
        canActivate: [permissionGuard],
        data: { base: 'my-attendance', open: true, permission: 'my-attendance.view' },
      },
      {
        path: 'my-attendance/:companyId/employee/:employeeId',
        component: EmployeeAttendance,
        canActivate: [permissionGuard],
        data: { permission: 'my-attendance.view' },
      },

      // ACCESS CONTROL (super admin only)
      {
        path: 'rbac/main-employee-permissions',
        component: MainEmployeePermission,
        canActivate: [permissionGuard],
        data: { superOnly: true },
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
