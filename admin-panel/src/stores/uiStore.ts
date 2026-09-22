import { create } from 'zustand';

interface UIState {
  theme: 'dark' | 'light' | 'system';
  sidebarCollapsed: boolean;
  setTheme: (theme: 'dark' | 'light' | 'system') => void;
  toggleSidebar: () => void;
}

export const useUIStore = create<UIState>((set) => ({
  theme: 'dark',
  sidebarCollapsed: false,
  setTheme: (theme) => set({ theme }),
  toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
}));