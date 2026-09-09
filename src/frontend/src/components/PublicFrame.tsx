import type { ReactNode } from 'react';
import { PublicAppShell, PublicHeader } from './ux/AppShells';

export type PublicSection = 'home' | 'new' | 'status' | 'staff' | 'system';

type PublicFrameProps = {
  children: ReactNode;
  current?: PublicSection;
};

export function AppHeader() {
  return <PublicHeader />;
}

export function PublicFrame({ children, current }: PublicFrameProps) {
  void current;
  return <PublicAppShell>{children}</PublicAppShell>;
}
