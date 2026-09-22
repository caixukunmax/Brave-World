import fs from 'fs';
import path from 'path';

export interface DiffResult {
  kind: 'N' | 'D' | 'E' | 'A';
  path: string[];
  lhs?: unknown;
  rhs?: unknown;
}

export class ConfigDiff {
  private tablesDir: string;

  constructor(dataDir: string) {
    this.tablesDir = path.join(dataDir, 'tables');
  }

  async diffTable(name: string): Promise<DiffResult[]> {
    const filePath = path.join(this.tablesDir, `${name}.json`);
    const dir = path.dirname(filePath);
    const base = path.basename(filePath);
    const backups = fs.readdirSync(dir)
      .filter(f => f.startsWith(base) && f.endsWith('.bak'))
      .sort().reverse();

    if (backups.length === 0) return [];

    const current = JSON.parse(fs.readFileSync(filePath, 'utf-8'));
    const previous = JSON.parse(fs.readFileSync(path.join(dir, backups[0]), 'utf-8'));
    return this.computeDiff(current, previous);
  }

  private computeDiff(current: unknown, previous: unknown, basePath: string[] = []): DiffResult[] {
    const results: DiffResult[] = [];
    if (current === previous) return results;

    if (typeof current !== typeof previous) {
      results.push({ kind: 'E', path: basePath, lhs: previous, rhs: current });
      return results;
    }

    if (Array.isArray(current) && Array.isArray(previous)) {
      const maxLen = Math.max(current.length, previous.length);
      for (let i = 0; i < maxLen; i++) {
        if (i >= previous.length) {
          results.push({ kind: 'N', path: [...basePath, String(i)], rhs: current[i] });
        } else if (i >= current.length) {
          results.push({ kind: 'D', path: [...basePath, String(i)], lhs: previous[i] });
        } else {
          results.push(...this.computeDiff(current[i], previous[i], [...basePath, String(i)]));
        }
      }
      return results;
    }

    if (typeof current === 'object' && current !== null && typeof previous === 'object' && previous !== null) {
      const curr = current as Record<string, unknown>;
      const prev = previous as Record<string, unknown>;
      const allKeys = new Set([...Object.keys(curr), ...Object.keys(prev)]);
      for (const key of allKeys) {
        if (!(key in prev)) {
          results.push({ kind: 'N', path: [...basePath, key], rhs: curr[key] });
        } else if (!(key in curr)) {
          results.push({ kind: 'D', path: [...basePath, key], lhs: prev[key] });
        } else {
          results.push(...this.computeDiff(curr[key], prev[key], [...basePath, key]));
        }
      }
      return results;
    }

    if (current !== previous) {
      results.push({ kind: 'E', path: basePath, lhs: previous, rhs: current });
    }
    return results;
  }
}