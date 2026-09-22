import fs from 'fs';
import path from 'path';
import type { DiffResult } from '../types';

export class ConfigDiff {
  private tablesDir = 'servercsharp/data/tables';

  private resolveTablesDir(): string {
    const dir = path.resolve(this.tablesDir);
    return fs.existsSync(dir) ? dir : path.resolve('servercsharp/data', 'tables');
  }

  async diffTable(name: string): Promise<DiffResult[]> {
    const tablesDir = this.resolveTablesDir();
    const filePath = path.join(tablesDir, `${name}.json`);
    const dir = path.dirname(filePath);
    const baseName = path.basename(filePath);

    const backups = fs.readdirSync(dir)
      .filter(f => f.startsWith(baseName) && f.endsWith('.bak'))
      .sort()
      .reverse();

    if (backups.length === 0) {
      return [];
    }

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
      const currentObj = current as Record<string, unknown>;
      const prevObj = previous as Record<string, unknown>;
      const allKeys = new Set([...Object.keys(currentObj), ...Object.keys(prevObj)]);

      for (const key of allKeys) {
        if (!(key in prevObj)) {
          results.push({ kind: 'N', path: [...basePath, key], rhs: currentObj[key] });
        } else if (!(key in currentObj)) {
          results.push({ kind: 'D', path: [...basePath, key], lhs: prevObj[key] });
        } else {
          results.push(...this.computeDiff(currentObj[key], prevObj[key], [...basePath, key]));
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