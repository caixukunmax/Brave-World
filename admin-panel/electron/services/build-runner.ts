import { exec } from 'child_process';
import type { BuildResult } from '../types';

export class BuildRunner {
  private async runCommand(cmd: string, cwd?: string): Promise<BuildResult> {
    return new Promise((resolve) => {
      const output: string[] = [];
      const errors: string[] = [];

      const child = exec(cmd, { cwd, maxBuffer: 10 * 1024 * 1024 }, (error, stdout, stderr) => {
        if (stdout) output.push(stdout);
        if (stderr) errors.push(stderr);
        resolve({
          success: !error && !stderr,
          exitCode: error?.code ?? 0,
          output: output.join('\n'),
          errors: errors.length > 0 ? errors : [],
        });
      });

      child.stdout?.on('data', (data: string) => {
        output.push(data);
      });

      child.stderr?.on('data', (data: string) => {
        errors.push(data);
      });
    });
  }

  async buildTables(): Promise<BuildResult> {
    return this.runCommand('powershell -ExecutionPolicy Bypass -File scripts/build.ps1 tables');
  }

  async buildProto(): Promise<BuildResult> {
    return this.runCommand('powershell -ExecutionPolicy Bypass -File scripts/build.ps1 proto');
  }

  async buildServer(): Promise<BuildResult> {
    return this.runCommand('dotnet build servercsharp/GameServer.sln');
  }

  async buildAll(): Promise<BuildResult> {
    return this.runCommand('powershell -ExecutionPolicy Bypass -File scripts/build.ps1 all');
  }
}