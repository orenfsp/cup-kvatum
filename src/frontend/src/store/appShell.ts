import { create } from 'zustand';

type AppShellState = {
  detailsVisible: boolean;
  toggleDetails: () => void;
};

export const useAppShellStore = create<AppShellState>((set) => ({
  detailsVisible: true,
  toggleDetails: () => set((state) => ({ detailsVisible: !state.detailsVisible })),
}));

