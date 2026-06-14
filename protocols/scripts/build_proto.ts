#!/usr/bin/env tsx
/**
 * Protocol Buffers 编译脚本 — C# 专用
 * 功能：编译 .proto 文件生成 C# 代码（客户端 + 服务端共用）
 */

import path from 'path';
import fs from 'fs';
import { execSync } from 'child_process';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);

const colors = {
  reset: '\x1b[0m',
  blue: '\x1b[34m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  red: '\x1b[31m',
};

function info(msg: string): void {
  console.log(`${colors.blue}[INFO]${colors.reset} ${msg}`);
}

function success(msg: string): void {
  console.log(`${colors.green}[SUCCESS]${colors.reset} ${msg}`);
}

function warn(msg: string): void {
  console.log(`${colors.yellow}[WARN]${colors.reset} ${msg}`);
}

function error(msg: string): void {
  console.error(`${colors.red}[ERROR]${colors.reset} ${msg}`);
}

interface PathsConfig {
  proto: {
    cs_dir: string;
    cs_server_dir?: string;
  };
}

function main(): void {
  const scriptDir = path.dirname(__filename);
  const baseDir = path.resolve(scriptDir, '..');
  const rootDir = path.resolve(baseDir, '..');

  // 加载路径配置
  const pathsPath = path.join(rootDir, 'paths.json');
  if (!fs.existsSync(pathsPath)) {
    error(`Paths config not found: ${pathsPath}`);
    process.exit(1);
  }
  const paths: PathsConfig = JSON.parse(fs.readFileSync(pathsPath, 'utf-8'));
  const outputCsDir = path.resolve(rootDir, paths.proto.cs_dir);
  const outputCsServerDir = paths.proto.cs_server_dir
    ? path.resolve(rootDir, paths.proto.cs_server_dir)
    : null;

  // 收集所有 proto 文件
  const protoDir = path.join(baseDir, 'proto');
  if (!fs.existsSync(protoDir)) {
    error(`Proto directory not found: ${protoDir}`);
    process.exit(1);
  }

  const allProtoFiles = fs.readdirSync(protoDir)
    .filter(f => f.endsWith('.proto'))
    .map(f => path.join(protoDir, f))
    .sort();

  if (allProtoFiles.length === 0) {
    error('No .proto files found');
    process.exit(1);
  }

  console.log('');
  console.log('========================================');
  console.log('  Compiling Protocol Buffers → C#');
  console.log('========================================');
  console.log('');

  info(`Found ${allProtoFiles.length} proto files:`);
  for (const file of allProtoFiles) {
    console.log(`  - ${path.basename(file)}`);
  }
  console.log('');

  // 查找 protoc
  let protocCmd: string | null = null;
  try {
    execSync('protoc --version', { stdio: 'ignore' });
    protocCmd = 'protoc';
  } catch {
    const localProtoc = path.join(baseDir, 'bin', 'protoc.exe');
    if (fs.existsSync(localProtoc)) {
      protocCmd = localProtoc;
    }
  }

  if (!protocCmd) {
    error('protoc not found. Please install protoc or place protoc.exe in protocols/bin/');
    error('Download: https://github.com/protocolbuffers/protobuf/releases');
    process.exit(1);
  }
  info(`Using protoc: ${protocCmd}`);
  console.log('');

  // 生成 C# 客户端代码
  console.log('----------------------------------------');
  info('Generating C# client files...');
  console.log('----------------------------------------');

  fs.mkdirSync(outputCsDir, { recursive: true });
  info(`Output directory: ${paths.proto.cs_dir}`);

  // 清空旧文件
  const oldCsFiles = fs.readdirSync(outputCsDir).filter(f => f.endsWith('.cs') && !f.endsWith('.uid'));
  for (const f of oldCsFiles) {
    fs.unlinkSync(path.join(outputCsDir, f));
  }

  const protoFileNames = allProtoFiles.map(f => path.basename(f));
  try {
    const cmd = [
      `"${protocCmd}"`,
      `--proto_path="${protoDir}"`,
      `--csharp_out="${outputCsDir}"`,
      ...protoFileNames.map(f => `"${f}"`),
    ].join(' ');
    execSync(cmd, { stdio: 'pipe' });
    for (const filename of protoFileNames.map(f => path.basename(f, '.proto'))) {
      success(`${filename}.cs`);
    }
  } catch (err) {
    error('C# client proto compilation failed');
    if (err instanceof Error) {
      const stderr = (err as any).stderr;
      if (stderr) console.error(stderr.toString());
      else console.error(err.message);
    }
    process.exit(1);
  }

  // 复制到服务端目录
  if (outputCsServerDir) {
    console.log('');
    console.log('----------------------------------------');
    info('Copying C# files to server directory...');
    console.log('----------------------------------------');

    fs.mkdirSync(outputCsServerDir, { recursive: true });
    const csFiles = fs.readdirSync(outputCsDir).filter(f => f.endsWith('.cs'));
    for (const f of csFiles) {
      fs.copyFileSync(path.join(outputCsDir, f), path.join(outputCsServerDir, f));
    }
    success(`C# server → ${paths.proto.cs_server_dir}`);
  }

  console.log('');
  console.log('========================================');
  success('Protocol compilation complete!');
  console.log('========================================');
  console.log('');
}

main();
