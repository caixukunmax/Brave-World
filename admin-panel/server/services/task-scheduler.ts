import fs from 'fs';
import path from 'path';

export interface ScheduledTask {
  id: string;
  name: string;
  cronExpression: string;
  command: string;
  enabled: boolean;
  lastRun?: string;
  lastResult?: string;
}

export class TaskScheduler {
  private dataDir: string;

  constructor(dataDir: string) {
    this.dataDir = dataDir;
  }

  private getFilePath(): string {
    const dir = path.join(this.dataDir, '..', '.admin-schedules');
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
    return path.join(dir, 'schedules.json');
  }

  private readAll(): ScheduledTask[] {
    const fp = this.getFilePath();
    if (!fs.existsSync(fp)) return [];
    return JSON.parse(fs.readFileSync(fp, 'utf-8'));
  }

  private writeAll(tasks: ScheduledTask[]): void {
    fs.writeFileSync(this.getFilePath(), JSON.stringify(tasks, null, 2), 'utf-8');
  }

  async list(): Promise<ScheduledTask[]> {
    return this.readAll();
  }

  async create(task: ScheduledTask): Promise<ScheduledTask> {
    const tasks = this.readAll();
    tasks.push(task);
    this.writeAll(tasks);
    return task;
  }

  async update(id: string, update: Partial<ScheduledTask>): Promise<void> {
    const tasks = this.readAll();
    const idx = tasks.findIndex(t => t.id === id);
    if (idx === -1) throw new Error(`Task ${id} not found`);
    tasks[idx] = { ...tasks[idx], ...update };
    this.writeAll(tasks);
  }

  async delete(id: string): Promise<void> {
    const tasks = this.readAll().filter(t => t.id !== id);
    this.writeAll(tasks);
  }
}