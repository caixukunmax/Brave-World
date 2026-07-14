import { create } from 'zustand';

interface ServerState {
  status: 'stopped' | 'starting' | 'running' | 'stopping' | 'error';
  pid: number | null;
  startTime: string | null;
  uptime: number;
  cpu: number;
  memory: number;
  onlinePlayers: number;
  error: string | null;
  setStatus: (status: Partial<ServerState>) => void;
}

export const useServerStore = create<ServerState>((set) => ({
  status: 'stopped',
  pid: null,
  startTime: null,
  uptime: 0,
  cpu: 0,
  memory: 0,
  onlinePlayers: 0,
  error: null,
  setStatus: (status) => set(status),
}));