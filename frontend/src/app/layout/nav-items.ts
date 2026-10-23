import { Role } from '../core/models/api.models';

export interface NavItem {
  label: string;
  icon: string;
  link: string;
  roles: Role[];
  exact?: boolean;
}

export const NAV_ITEMS: NavItem[] = [
  {
    label: 'Mes demandes',
    icon: 'folder_open',
    link: '/client/demandes',
    roles: ['Client'],
    exact: true,
  },
  {
    label: 'Nouvelle demande',
    icon: 'add_circle',
    link: '/client/demandes/nouvelle',
    roles: ['Client'],
  },
  {
    label: 'Profil financier',
    icon: 'account_balance_wallet',
    link: '/client/profil',
    roles: ['Client'],
  },
  { label: 'Simulateur', icon: 'calculate', link: '/', roles: ['Client'], exact: true },
  { label: "File d'attente", icon: 'inbox', link: '/analyste/file', roles: ['Analyst', 'Admin'] },
  {
    label: 'Tableau de bord',
    icon: 'insights',
    link: '/analyste/tableau-de-bord',
    roles: ['Analyst', 'Admin'],
  },
  { label: 'Utilisateurs', icon: 'group', link: '/admin/utilisateurs', roles: ['Admin'] },
  { label: 'Produits de crédit', icon: 'tune', link: '/admin/produits', roles: ['Admin'] },
];

export function navItemsFor(role: Role | null): NavItem[] {
  return role ? NAV_ITEMS.filter((item) => item.roles.includes(role)) : [];
}
