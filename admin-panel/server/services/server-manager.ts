import { ChildProcess, spawn } from 'child_process';
import path from 'path';
import type { ServerProfile } from './profile-manager';

export interface ServerStatus {
  profileId: string;
  status: 'stopped' | 'starting' | 'running' | 'stopping' | 'error';
  pid: number | null;
  startTime: string | null;
  uptime: number;
  cpu: number;
  memory: number;
  error: string | null;
}

interface Instance {
  profileId: string;
  process: ChildProcess | null;
  status: ServerStatus;
  startTime: Date | null;
  uptimeTimer: ReturnType<typeof setInterval> | null;
  outputLines: string[];
}

export class ServerManager {
  private instances = new Map<string, Instance>();
  private maxOutputLines = 500;

  private getOrCreate(profileId: string): Instance {
    let inst = this.instances.get(profileId);
    if (!inst) {
      inst = {
        profileId,
        process: null,
        status: { profileId, status: 'stopped', pid: null, startTime: null, uptime: 0, cpu: 0, memory: 0, error: null },
        startTime: null,
        uptimeTimer: null,
        outputLines: [],
      };
      this.instances.set(profileId, inst);
    }
    return inst;
  }

  getAllStatuses(): ServerStatus[] {
    return Array.from(this.instances.values()).map(i => ({ ...i.status }));
  }

  getStatus(profileId: string): ServerStatus {
    const inst = this.instances.get(profileId);
    return inst ? { ...inst.status } : { profileId, status: 'stopped', pid: null, startTime: null, uptime: 0, cpu: 0, memory: 0, error: null };
  }

  getOutput(profileId: string): string[] {
    return [...(this.instances.get(profileId)?.outputLines || [])];
  }

  async start(profile: ServerProfile): Promise<{ profileId: string; pid: number }> {
    const inst = this.getOrCreate(profile.id);
    if (inst.process) throw new Error(`Server "${profile.id}" is already running`);

    inst.status = { ...inst.status, status: 'starting', error: null };
    inst.outputLines = [];

    const exePath = path.resolve(profile.repoRoot, profile.serverExePath);
    const cwd = path.dirname(exePath);
    const env = { ...process.env, ...(profile as any).envVars };

    inst.process = spawn(exePath, [], { cwd, env, stdio: ['pipe', 'pipe', 'pipe'] });

    inst.status.pid = inst.process.pid ?? null;
    inst.startTime = new Date();
    inst.status.startTime = inst.startTime.toISOString();
    inst.status.status = 'running';

    inst.process.stdout?.on('data', (data: Buffer) => {
      const lines = data.toString().split('\n').filter(Boolean);
      for (const line of lines) {
        inst.outputLines.push(line);
        if (inst.outputLines.length > this.maxOutputLines) inst.outputLines.shift();
      }
    });

    inst.process.stderr?.on('data', (data: Buffer) => {
      const lines = data.toString().split('\n').filter(Boolean);
      for (const line of lines) {
        inst.outputLines.push(`[STDERR] ${line}`);
        if (inst.outputLines.length > this.maxOutputLines) inst.outputLines.shift();
      }
    });

    inst.process.on('close', (code) => {
      inst.status.status = 'stopped';
      inst.status.pid = null;
      inst.status.error = code !== 0 ? `Exited with code ${code}` : null;
      inst.process = null;
      inst.startTime = null;
      if (inst.uptimeTimer) { clearInterval(inst.uptimeTimer); inst.uptimeTimer = null; }
    });

    inst.process.on('error', (err) => {
      inst.status.status = 'error';
      inst.status.error = err.message;
    });

    inst.uptimeTimer = setInterval(() => {
      if (inst.startTime) {
        inst.status.uptime = Math.floor((Date.now() - inst.startTime.getTime()) / 1000);
      }
    }, 1000);

    return { profileId: profile.id, pid: inst.process.pid! };
  }

  async stop(profileId: string): Promise<void> {
    const inst = this.instances.get(profileId);
    if (!inst?.process) throw new Error(`Server "${profileId}" is not running`);
    inst.status.status = 'stopping';

    return new Promise((resolve) => {
      const forceKillTimeout = setTimeout(() => {
        if (inst.process) inst.process.kill('SIGKILL');
      }, 5000);

      inst.process!.on('close', () => {
        clearTimeout(forceKillTimeout);
        resolve();
      });

      inst.process!.kill('SIGTERM');
    });
  }

  async restart(profile: ServerProfile): Promise<void> {
    try { await this.stop(profile.id); } catch { /* not running */ }
    await this.start(profile);
  }

  appendOutput(profileId: string, line: string): void {
    const inst = this.getOrCreate(profileId);
    inst.outputLines.push(line);
    if (inst.outputLines.length > this.maxOutputLines) inst.outputLines.shift();
  }

  sendCommand(profileId: string, cmd: string): void {
    const inst = this.instances.get(profileId);
    if (!inst?.process?.stdin) throw new Error(`Server "${profileId}" is not running`);
    inst.process.stdin.write(cmd + '\n');
  }
}