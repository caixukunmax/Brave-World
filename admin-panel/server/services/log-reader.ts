import fs from 'fs';
import path from 'path';

export interface LogLine {
  timestamp: string;
  level: string;
  message: string;
  raw: string;
}

export class LogReader {
  private repoRoot: string;

  constructor(repoRoot: string) {
    this.repoRoot = repoRoot;
  }

  private findLogFile(): string | null {
    const candidates = [
      path.join(this.repoRoot, 'servercsharp', 'logs'),
      path.join(this.repoRoot, 'servercsharp', 'src', 'GameServer', 'logs'),
      path.join(this.repoRoot, 'servercsharp', 'src', 'GameServer', 'bin', 'Debug', 'net8.0', 'logs'),
    ];
    for (const dir of candidates) {
      if (fs.existsSync(dir)) {
        const files = fs.readdirSync(dir).filter(f => f.startsWith('server-') && f.endsWith('.log'));
        if (files.length > 0) {
          files.sort().reverse();
          return path.join(dir, files[0]);
        }
      }
    }
    return null;
  }

  private parseLine(line: string): LogLine {
    const match = line.match(/^\[(\w+)\]\s*(?:\[([^\]]+)\]\s*)?(.*)/);
    const level = match?.[1] || 'INF';
    const timestamp = match?.[2] || '';
    const message = match?.[3] || line;
    return { timestamp, level, message, raw: line };
  }

  async getHistory(lines: number = 200): Promise<LogLine[]> {
    const logFile = this.findLogFile();
    if (!logFile) return [];

    const content = fs.readFileSync(logFile, 'utf-8');
    const allLines = content.split('\n').filter(Boolean);
    const recent = allLines.slice(-lines);
    return recent.map(l => this.parseLine(l));
  }

  async tail(count: number = 50): Promise<LogLine[]> {
    return this.getHistory(count);
  }
}