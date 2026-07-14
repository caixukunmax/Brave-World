import fs from 'fs';
import path from 'path';
import type { ValidationResult } from '../types';

export class ConfigValidator {
  private tablesDir = 'servercsharp/data/tables';

  private resolveTablesDir(): string {
    const dir = path.resolve(this.tablesDir);
    return fs.existsSync(dir) ? dir : path.resolve('servercsharp/data', 'tables');
  }

  private loadTable(name: string): unknown[] {
    const tablesDir = this.resolveTablesDir();
    const filePath = path.join(tablesDir, `${name}.json`);
    if (!fs.existsSync(filePath)) return [];
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  }

  private getIds(data: unknown[]): Set<number> {
    return new Set(data.map((row: any) => row.id).filter(Boolean));
  }

  async validateTable(name: string): Promise<ValidationResult[]> {
    const results: ValidationResult[] = [];
    const data = this.loadTable(name);

    const idSet = new Set<number>();
    for (const row of data as any[]) {
      const id = row.id;
      if (id === undefined || id === null) {
        results.push({ field: 'id', rowId: 'unknown', message: 'Row missing id field', severity: 'error' });
        continue;
      }
      if (idSet.has(id)) {
        results.push({ field: 'id', rowId: id, message: `Duplicate id: ${id}`, severity: 'error' });
      }
      idSet.add(id);
    }

    switch (name) {
      case 'common_tbmapmonster': {
        const monsterIds = this.getIds(this.loadTable('common_tbmonster'));
        const aiIds = this.getIds(this.loadTable('common_tbai'));
        const mapIds = this.getIds(this.loadTable('common_tbmapconfig'));
        for (const row of data as any[]) {
          if (row.monster_id && !monsterIds.has(row.monster_id)) {
            results.push({ field: 'monster_id', rowId: row.id, message: `monster_id ${row.monster_id} not found in common_tbmonster`, severity: 'error' });
          }
          if (row.ai_id && !aiIds.has(row.ai_id)) {
            results.push({ field: 'ai_id', rowId: row.id, message: `ai_id ${row.ai_id} not found in common_tbai`, severity: 'error' });
          }
          if (row.map_id && !mapIds.has(row.map_id)) {
            results.push({ field: 'map_id', rowId: row.id, message: `map_id ${row.map_id} not found in common_tbmapconfig`, severity: 'error' });
          }
        }
        break;
      }
      case 'common_tbmapnpc': {
        const npcIds = this.getIds(this.loadTable('common_tbnpc'));
        for (const row of data as any[]) {
          if (row.npc_id && !npcIds.has(row.npc_id)) {
            results.push({ field: 'npc_id', rowId: row.id, message: `npc_id ${row.npc_id} not found in common_tbnpc`, severity: 'error' });
          }
        }
        break;
      }
      case 'common_tbmonster': {
        const skillIds = this.getIds(this.loadTable('common_tbskill'));
        const dropGroupIds = this.getIds(this.loadTable('common_tbdropgroup'));
        for (const row of data as any[]) {
          if (row.skills) {
            for (const skillId of row.skills) {
              if (!skillIds.has(skillId)) {
                results.push({ field: 'skills', rowId: row.id, message: `skill_id ${skillId} not found in common_tbskill`, severity: 'warning' });
              }
            }
          }
          if (row.drop_group_id && !dropGroupIds.has(row.drop_group_id)) {
            results.push({ field: 'drop_group_id', rowId: row.id, message: `drop_group_id ${row.drop_group_id} not found in common_tbdropgroup`, severity: 'warning' });
          }
        }
        break;
      }
    }

    return results;
  }
}