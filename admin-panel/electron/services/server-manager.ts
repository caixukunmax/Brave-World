import { ChildProcess, spawn } from 'child_process';
import path from 'path';
import type { ServerProfile, ServerStatus } from '../types';

export class ServerManager {
  private process: ChildProcess | null = null;
  private status: ServerStatus = {
    status: 'stopped',
    pid: null,
    startTime: null,
    uptime: 0,
    cpu: 0,
    memory: 0,
    error: null,
  };
  private startTime: Date | null = null;
  private uptimeTimer: ReturnType<typeof setInterval> | null = null;
  private outputListeners: Array<(line: string) => void> = [];
  private statusListeners: Array<(status: ServerStatus) => void> = [];

  onOutput(callback: (line: string) => void): () => void {
    this.outputListeners.push(callback);
    return () => {
      this.outputListeners = this.outputListeners.filter(cb => cb !== callback);
    };
  }

  onStatusChange(callback: (status: ServerStatus) => void): () => void {
    this.statusListeners.push(callback);
    return () => {
      this.statusListeners = this.statusListeners.filter(cb => cb !== callback);
    };
  }

  private emitOutput(line: string): void {
    for (const cb of this.outputListeners) cb(line);
  }

  private emitStatus(): void {
    for (const cb of this.statusListeners) cb({ ...this.status });
  }

  private updateUptime(): void {
    if (this.startTime) {
      this.status.uptime = Math.floor((Date.now() - this.startTime.getTime()) / 1000);
      this.emitStatus();
    }
  }

  async start(profile: ServerProfile): Promise<{ pid: number }> {
    if (this.process) {
      throw new Error('Server is already running');
    }

    this.status = { ...this.status, status: 'starting', error: null };
    this.emitStatus();

    const exePath = path.resolve(profile.repoRoot, profile.serverExePath);
    const cwd = path.dirname(exePath);
    const env = { ...process.env, ...profile.envVars };

    this.process = spawn(exePath, [], {
      cwd,
      env,
      stdio: ['pipe', 'pipe', 'pipe'],
    });

    this.status.pid = this.process.pid ?? null;
    this.startTime = new Date();
    this.status.startTime = this.startTime.toISOString();
    this.status.status = 'running';
    this.emitStatus();

    this.process.stdout?.on('data', (data: Buffer) => {
      const lines = data.toString().split('\n').filter(Boolean);
      for (const line of lines) {
        this.emitOutput(line);
      }
    });

    this.process.stderr?.on('data', (data: Buffer) => {
      const lines = data.toString().split('\n').filter(Boolean);
      for (const line of lines) {
        this.emitOutput(`[STDERR] ${line}`);
      }
    });

    this.process.on('close', (code) => {
      this.status.status = 'stopped';
      this.status.pid = null;
      this.status.error = code !== 0 ? `Process exited with code ${code}` : null;
      this.process = null;
      this.startTime = null;
      if (this.uptimeTimer) {
        clearInterval(this.uptimeTimer);
        this.uptimeTimer = null;
      }
      this.emitStatus();

      if (profile.autoRestart && code !== 0) {
        setTimeout(() => this.start(profile), 5000);
      }
    });

    this.process.on('error', (err) => {
      this.status.status = 'error';
      this.status.error = err.message;
      this.emitStatus();
    });

    this.uptimeTimer = setInterval(() => this.updateUptime(), 1000);

    return { pid: this.process.pid! };
  }

  async stop(): Promise<void> {
    if (!this.process) {
      throw new Error('Server is not running');
    }

    this.status.status = 'stopping';
    this.emitStatus();

    return new Promise((resolve) => {
      const forceKillTimeout = setTimeout(() => {
        if (this.process) {
          this.process.kill('SIGKILL');
        }
      }, 5000);

      this.process!.on('close', () => {
        clearTimeout(forceKillTimeout);
        resolve();
      });

      this.process!.kill('SIGTERM');
    });
  }

  async restart(profile: ServerProfile): Promise<void> {
    await this.stop();
    await this.start(profile);
  }

  sendCommand(cmd: string): void {
    if (!this.process?.stdin) {
      throw new Error('Server is not running or stdin is not available');
    }
    this.process.stdin.write(cmd + '\n');
  }

  getStatus(): ServerStatus {
    return { ...this.status };
  }
}