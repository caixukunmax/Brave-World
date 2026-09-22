import fs from 'fs';
import path from 'path';

export interface ServerProfile {
  id: string;
  name: string;
  repoRoot: string;
  serverExePath: string;
  mongoHost: string;
  mongoPort: number;
  mongoDb: string;
  httpApiPort: number;
  gatewayPort: number;
  autoRestart: boolean;
}

export class ProfileManager {
  private profilesDir: string;

  constructor(dataDir: string) {
    this.profilesDir = path.join(dataDir, '..', '.admin-profiles');
    if (!fs.existsSync(this.profilesDir)) {
      fs.mkdirSync(this.profilesDir, { recursive: true });
    }
  }

  private getFilePath(): string {
    return path.join(this.profilesDir, 'profiles.json');
  }

  private readAll(): ServerProfile[] {
    const filePath = this.getFilePath();
    if (!fs.existsSync(filePath)) return [];
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  private writeAll(profiles: ServerProfile[]): void {
    fs.writeFileSync(this.getFilePath(), JSON.stringify(profiles, null, 2), 'utf-8');
  }

  async list(): Promise<ServerProfile[]> {
    return this.readAll();
  }

  async get(id: string): Promise<ServerProfile | null> {
    return this.readAll().find(p => p.id === id) ?? null;
  }

  async create(profile: ServerProfile): Promise<ServerProfile> {
    const profiles = this.readAll();
    profiles.push(profile);
    this.writeAll(profiles);
    return profile;
  }

  async update(id: string, update: Partial<ServerProfile>): Promise<ServerProfile> {
    const profiles = this.readAll();
    const idx = profiles.findIndex(p => p.id === id);
    if (idx === -1) throw new Error(`Profile ${id} not found`);
    profiles[idx] = { ...profiles[idx], ...update };
    this.writeAll(profiles);
    return profiles[idx];
  }

  async delete(id: string): Promise<void> {
    const profiles = this.readAll().filter(p => p.id !== id);
    this.writeAll(profiles);
  }
}