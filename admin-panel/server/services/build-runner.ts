import { spawn } from 'child_process';

export interface BuildResult {
  success: boolean;
  exitCode: number;
  output: string;
  errors: string[];
}

export class BuildRunner {
  private repoRoot: string;

  constructor(repoRoot: string) {
    this.repoRoot = repoRoot;
  }

  private runCommand(
    cmd: string,
    args: string[],
    cwd?: string,
    onLine?: (line: string) => void,
  ): Promise<BuildResult> {
    return new Promise((resolve) => {
      const output: string[] = [];
      const errors: string[] = [];

      const child = spawn(cmd, args, {
        cwd: cwd || this.repoRoot,
        shell: true,
        stdio: ['ignore', 'pipe', 'pipe'],
      });

      child.stdout?.on('data', (data: Buffer) => {
        const text = data.toString();
        output.push(text);
        if (onLine) {
          text.split('\n').filter(Boolean).forEach(onLine);
        }
      });

      child.stderr?.on('data', (data: Buffer) => {
        const text = data.toString();
        errors.push(text);
        if (onLine) {
          text.split('\n').filter(Boolean).forEach(l => onLine(`[ERR] ${l}`));
        }
      });

      child.on('close', (code) => {
        resolve({
          success: code === 0 && errors.length === 0,
          exitCode: code ?? 0,
          output: output.join(''),
          errors: errors.length > 0 ? errors : [],
        });
      });

      child.on('error', (err) => {
        resolve({
          success: false,
          exitCode: -1,
          output: output.join(''),
          errors: [...errors, err.message],
        });
      });
    });
  }

  private ps(args: string, onLine?: (line: string) => void): Promise<BuildResult> {
    return this.runCommand('powershell', ['-ExecutionPolicy', 'Bypass', '-File', ...args.split(' ')], undefined, onLine);
  }

  async buildTables(onLine?: (line: string) => void): Promise<BuildResult> {
    return this.ps('scripts/build.ps1 tables', onLine);
  }

  async buildProto(onLine?: (line: string) => void): Promise<BuildResult> {
    return this.ps('scripts/build.ps1 proto', onLine);
  }

  async buildServer(onLine?: (line: string) => void): Promise<BuildResult> {
    return this.runCommand('dotnet', ['build', 'servercsharp/GameServer.sln'], undefined, onLine);
  }

  async buildAll(onLine?: (line: string) => void): Promise<BuildResult> {
    return this.ps('scripts/build.ps1 all', onLine);
  }

  async runRestartBat(onLine?: (line: string) => void): Promise<BuildResult> {
    // 直接运行 restart.bat，和手动执行完全一致
    return this.runCommand('cmd', ['/c', 'servercsharp\\restart.bat'], undefined, onLine);
  }
}