import path from 'path';
import fs from 'fs';
import type { ServerProfile } from '../types';

export class ProfileManager {
  private _profilesDir: string | null = null;

  private get profilesDir(): string {
    if (!this._profilesDir) {
      const { app } = require('electron');
      this._profilesDir = path.join(app.getPath('userData'), 'profiles');
      if (!fs.existsSync(this._profilesDir)) {
        fs.mkdirSync(this._profilesDir, { recursive: true });
      }
    }
    return this._profilesDir;
  }

  private getFilePath(): string {
    return path.join(this.profilesDir, 'profiles.json');
  }

  private readAll(): ServerProfile[] {
    const filePath = this.getFilePath();
    if (!fs.existsSync(filePath)) return [];
    const raw = fs.readFileSync(filePath, 'utf-8');
    return JSON.parse(raw);
  }

  private writeAll(profiles: ServerProfile[]): void {
    fs.writeFileSync(this.getFilePath(), JSON.stringify(profiles, null, 2), 'utf-8');
  }

  private getActiveFilePath(): string {
    return path.join(this.profilesDir, 'active.json');
  }

  async list(): Promise<ServerProfile[]> {
    return this.readAll();
  }

  async get(id: string): Promise<ServerProfile | null> {
    const profiles = this.readAll();
    return profiles.find(p => p.id === id) ?? null;
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

  async setActive(id: string): Promise<void> {
    fs.writeFileSync(this.getActiveFilePath(), JSON.stringify({ activeId: id }), 'utf-8');
  }

  async getActive(): Promise<ServerProfile | null> {
    const filePath = this.getActiveFilePath();
    if (!fs.existsSync(filePath)) return null;
    const { activeId } = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
    return this.get(activeId);
  }
}