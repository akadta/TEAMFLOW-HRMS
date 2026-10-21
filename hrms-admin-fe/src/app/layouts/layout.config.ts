import { Component, computed, input } from '@angular/core';

// ================= ICONS (add/edit here only) =================
// Each icon = array of SVG path "d" strings (24x24, stroke style)
export const ICONS = {
  dashboard: ['M3 3h7v9H3z', 'M14 3h7v5h-7z', 'M14 12h7v9h-7z', 'M3 16h7v5H3z'],
  users: [
    'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2',
    'M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0z',
    'M23 21v-2a4 4 0 0 0-3-3.87',
    'M16 3.13a4 4 0 0 1 0 7.75',
  ],
  user: ['M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2', 'M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0z'],
  calendar: [
    'M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z',
    'M16 2v4',
    'M8 2v4',
    'M3 10h18',
  ],
  coffee: [
    'M18 8h1a4 4 0 0 1 0 8h-1',
    'M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8z',
    'M6 1v3',
    'M10 1v3',
    'M14 1v3',
  ],
  briefcase: [
    'M4 7h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2z',
    'M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16',
  ],
  dollar: ['M12 1v22', 'M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'],
  fileText: [
    'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z',
    'M14 2v6h6',
    'M16 13H8',
    'M16 17H8',
    'M10 9H8',
  ],
  settings: [
    'M4 21v-7',
    'M4 10V3',
    'M12 21v-9',
    'M12 8V3',
    'M20 21v-5',
    'M20 12V3',
    'M1 14h6',
    'M9 8h6',
    'M17 16h6',
  ],
  menu: ['M3 12h18', 'M3 6h18', 'M3 18h18'],
  search: ['M21 21l-4.35-4.35', 'M19 11a8 8 0 1 1-16 0 8 8 0 0 1 16 0z'],
  bell: ['M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9', 'M13.73 21a2 2 0 0 1-3.46 0'],
  chevronDown: ['M6 9l6 6 6-6'],
  logOut: ['M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4', 'M16 17l5-5-5-5', 'M21 12H9'],

  // ---- new icons ----
  building: [
    'M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18z',
    'M6 12H4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2',
    'M18 9h2a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-2',
    'M10 6h4',
    'M10 10h4',
    'M10 14h4',
    'M10 18h4',
  ],
  userPlus: [
    'M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2',
    'M12.5 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0z',
    'M20 8v6',
    'M23 11h-6',
  ],
  hierarchy: ['M9 2h6v5H9z', 'M2 17h6v5H2z', 'M16 17h6v5h-6z', 'M12 7v5', 'M5 17v-5h14v5'],
  idCard: [
    'M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z',
    'M11 10a2 2 0 1 1-4 0 2 2 0 0 1 4 0z',
    'M6 16c0-1.5 1.3-2.5 3-2.5s3 1 3 2.5',
    'M15 9h3',
    'M15 13h3',
  ],
  shield: ['M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z'],
  list: ['M8 6h13', 'M8 12h13', 'M8 18h13', 'M3 6h.01', 'M3 12h.01', 'M3 18h.01'],
  plusCircle: ['M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0z', 'M12 8v8', 'M8 12h8'],

  // ---- unique icons for menu items ----
  home: ['M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z', 'M9 22V12h6v10'],
  clock: ['M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0z', 'M12 6v6l4 2'],
  barChart: ['M18 20V10', 'M12 20V4', 'M6 20v-6'],
  clipboardCheck: [
    'M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2',
    'M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z',
    'M9 14l2 2 4-4',
  ],
  lock: [
    'M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z',
    'M7 11V7a5 5 0 0 1 10 0v4',
  ],
} satisfies Record<string, string[]>;

export type IconName = keyof typeof ICONS;

// ================= ICON COMPONENT =================
// usage: <app-icon name="users" class="h-4 w-4" />
@Component({
  selector: 'app-icon',
  template: `
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.8"
      stroke-linecap="round"
      stroke-linejoin="round"
      class="h-full w-full"
      aria-hidden="true"
    >
      @for (d of paths(); track $index) {
        <path [attr.d]="d" />
      }
    </svg>
  `,
  host: { class: 'inline-block shrink-0' },
})
export class Icon {
  name = input.required<IconName>();
  paths = computed(() => ICONS[this.name()]);
}

// ================= SIDEBAR MENU (add menus / submenus here only) =================
export interface MenuChild {
  label: string;
  route: string;
  permission?: string | string[]; // any-of; leave out = everyone
  superOnly?: boolean;
}
export interface MenuItem {
  id: string;
  label: string;
  icon: IconName;
  route?: string; // leaf item
  children?: MenuChild[]; // submenu
  badge?: string;
  permission?: string | string[];
  superOnly?: boolean;
}
export interface MenuGroup {
  title: string;
  items: MenuItem[];
}

export const MENU: MenuGroup[] = [
  // ---------- 1. MAIN ----------
  {
    title: 'Main',
    items: [{ id: 'dashboard', label: 'Dashboard', icon: 'dashboard', route: '/dashboard' }],
  },

  // ---------- 2. COMPANY AND EMPLOYEES ----------
  {
    title: 'Company & Employees',
    items: [
      {
        id: 'company',
        label: 'Company',
        icon: 'building',
        children: [
          { label: 'Register Company', route: 'company/register', permission: 'company.register' },
          { label: 'Manage Company', route: 'company/list', permission: 'company.list' },
          { label: 'Company Roles', route: 'company/roles', permission: 'company.roles' },
        ],
      },
      {
        id: 'employees',
        label: 'Employees',
        icon: 'users',
        children: [
          {
            label: 'Register Employee',
            route: 'employee/register',
            permission: 'employees.register',
          },
          { label: 'Manage Employee', route: 'employee/list', permission: 'employees.list' },
        ],
      },
    ],
  },

  // ---------- 3. MY COMPANIES ----------
  {
    title: 'My Companies',
    items: [
      {
        id: 'my-company',
        label: 'My Company',
        icon: 'home',
        route: '/my-companies',
        permission: 'my-companies.view',
      },
      {
        id: 'my-attendance',
        label: 'Attendance',
        icon: 'clock',
        route: '/my-attendance',
        permission: 'my-attendance.view',
      },
      {
        id: 'my-leave',
        label: 'Leave Requests',
        icon: 'coffee',
        route: '/leave/approvals',
        permission: 'leave.approval',
      },
      {
        id: 'team',
        label: 'Team Overview',
        icon: 'barChart',
        route: '/team',
        permission: 'team.view',
      },
      {
        id: 'assign-task',
        label: 'Assign Task',
        icon: 'clipboardCheck',
        route: '/tasks/assign',
        permission: 'tasks.assign',
      },
      {
        id: 'task-approvals',
        label: 'Task Approvals',
        icon: 'list',
        route: '/tasks/approvals',
        permission: 'tasks.approvals',
      },
    ],
  },

  // ---------- 4. ALLOCATIONS ----------
  {
    title: 'Allocations',
    items: [
      {
        id: 'allocations',
        label: 'Allocations',
        icon: 'hierarchy',
        children: [
          {
            label: 'Reporting Managers',
            route: 'allocations/reporting-managers',
            permission: 'allocations.reporting-managers',
          },
          { label: 'Hierarchy Tree', route: 'hierarchy/tree', permission: 'hierarchy.tree' },
        ],
      },
    ],
  },

  // ---------- 5. MAIN TEAM ----------
  {
    title: 'Main Team',
    items: [
      { id: 'roles', label: 'Roles', icon: 'shield', route: '/roles', permission: 'roles.manage' },
      {
        id: 'main-employees',
        label: 'Main Employees',
        icon: 'briefcase',
        children: [
          {
            label: 'Register Main Employee',
            route: 'main-employee/register',
            permission: 'main-employees.register',
          },
          {
            label: 'Manage Main Employees',
            route: 'main-employee/list',
            permission: 'main-employees.list',
          },
        ],
      },
    ],
  },

  // ---------- 6. SETTINGS (all settings in one submenu) ----------
  {
    title: 'Settings',
    items: [
      {
        id: 'settings',
        label: 'Settings',
        icon: 'settings',
        children: [
          {
            label: 'Holiday Calendar',
            route: 'attendance/settings',
            permission: 'attendance.holidays',
          },
          {
            label: 'Attendance Timing',
            route: 'attendance/timing',
            permission: 'attendance.timing',
          },
          {
            label: 'Leave Settings',
            route: 'attendance/leave-settings',
            permission: 'leave.setting',
          },
          { label: 'Task Settings', route: 'tasks/settings', permission: 'tasks.settings' },
        ],
      },
    ],
  },

  // ---------- 7. ACCESS CONTROL ----------
  {
    title: 'Access Control',
    items: [
      {
        id: 'access-control',
        label: 'Access Control',
        icon: 'lock',
        children: [
          {
            label: 'Employee Permissions',
            route: 'rbac/main-employee-permissions',
            superOnly: true,
          },
        ],
      },
    ],
  },
];

// hides items the user cannot access; a group with no visible items disappears
export function filterMenu(
  groups: MenuGroup[],
  can: (permission?: string | string[], superOnly?: boolean) => boolean,
): MenuGroup[] {
  return groups
    .map((g) => ({
      ...g,
      items: g.items
        .map((i) =>
          i.children
            ? { ...i, children: i.children.filter((c) => can(c.permission, c.superOnly)) }
            : i,
        )
        .filter((i) => (i.children ? i.children.length > 0 : can(i.permission, i.superOnly))),
    }))
    .filter((g) => g.items.length > 0);
}

// ================= NAVBAR AVATAR DROPDOWN =================
export const USER_MENU: { label: string; icon: IconName; route: string }[] = [
  //   { label: 'My Profile', icon: 'user', route: '/profile' },
  //   { label: 'Settings', icon: 'settings', route: '/settings' },
];
