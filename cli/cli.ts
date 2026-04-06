#!/usr/bin/env node
/**
 * tslua CLI - 游戏服务端构建工具
 *
 * Usage:
 *   node cli/cli.ts <command> [args...]
 *
 * Commands:
 *   build [proto|tables|server|all]   构建各组件（默认 all）
 *   new <name>                         创建新服务
 *   test [args...]                     运行 jest 测试
 *   clean [proto|tables|server|all]    清理构建输出（默认 all）
 *   status                             查看项目状态
 */

import * as fs from 'fs';
import * as path from 'path';
import { spawnSync } from 'child_process';

// ═══════════════════════════════════════════════════════════════════
// 颜色
// ═══════════════════════════════════════════════════════════════════
const C = {
  reset: '\x1b[0m', bold: '\x1b[1m', dim: '\x1b[2m',
  blue: '\x1b[34m', green: '\x1b[32m', yellow: '\x1b[33m', red: '\x1b[31m', cyan: '\x1b[36m', gray: '\x1b[90m',
};
const log = {
  info: (m: string) => console.log(`${C.blue}[INFO]${C.reset} ${m}`),
  ok:   (m: string) => console.log(`${C.green}[DONE]${C.reset} ${m}`),
  warn: (m: string) => console.log(`${C.yellow}[WARN]${C.reset} ${m}`),
  err:  (m: string) => console.error(`${C.red}[ERR]${C.reset} ${m}`),
  dim:  (m: string) => console.log(`${C.gray}${m}${C.reset}`),
};

// ═══════════════════════════════════════════════════════════════════
// 路径
// ═══════════════════════════════════════════════════════════════════
function findRoot(): string {
  let dir = __dirname;
  for (let i = 0; i < 10; i++) {
    if (fs.existsSync(path.join(dir, 'paths.json'))) return dir;
    const p = path.dirname(dir);
    if (p === dir) break;
    dir = p;
  }
  log.err('Cannot find paths.json (not in a tslua2 project?)');
  process.exit(1);
}

interface Paths {
  root: string;
  skynet_lua_dir: string;
  server_src_dir: string;
  proto: { desc_dir: string; ts_dir: string };
  tables: { lua_code_dir: string; lua_data_dir: string; json_data_dir: string };
}

function loadPaths(): Paths {
  const root = findRoot();
  const raw = JSON.parse(fs.readFileSync(path.join(root, 'paths.json'), 'utf-8'));
  return {
    root,
    skynet_lua_dir: path.resolve(root, raw.skynet_lua_dir),
    server_src_dir: path.resolve(root, raw.server_src_dir),
    proto: {
      desc_dir: path.resolve(root, raw.proto.desc_dir),
      ts_dir:   path.resolve(root, raw.proto.ts_dir),
    },
    tables: {
      lua_code_dir: path.resolve(root, raw.tables.lua_code_dir),
      lua_data_dir: path.resolve(root, raw.tables.lua_data_dir),
      json_data_dir: path.resolve(root, raw.tables.json_data_dir),
    },
  };
}

// ═══════════════════════════════════════════════════════════════════
// 工具
// ═══════════════════════════════════════════════════════════════════
function run(cmd: string, args: string[], opts?: { cwd?: string }): number {
  const r = spawnSync(cmd, args, { stdio: 'inherit', shell: true, ...opts });
  return r.status ?? 1;
}

function rmDir(label: string, dir: string): void {
  if (fs.existsSync(dir)) {
    fs.rmSync(dir, { recursive: true, force: true });
    log.ok(`Removed ${label}`);
  } else {
    log.dim(`  skip (not found): ${dir}`);
  }
}

function countFiles(dir: string, ext: string): number {
  if (!fs.existsSync(dir)) return 0;
  let n = 0;
  function walk(d: string) {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      if (e.isDirectory()) walk(path.join(d, e.name));
      else if (e.name.endsWith(ext)) n++;
    }
  }
  walk(dir);
  return n;
}

// ═══════════════════════════════════════════════════════════════════
// Commands
// ═══════════════════════════════════════════════════════════════════

function cmdBuild(target: string): void {
  const p = loadPaths();
  const steps: [string, () => number][] = [];

  if (target === 'proto' || target === 'all')
    steps.push(['proto', () => run('npm', ['run', 'build'], { cwd: path.join(p.root, 'protocols') })]);
  if (target === 'tables' || target === 'all')
    steps.push(['tables', () => run('npm', ['run', 'build'], { cwd: path.join(p.root, 'tables') })]);
  if (target === 'server' || target === 'all')
    steps.push(['server', () => run('npx', ['tstl', '-p', 'tsconfig.json'], { cwd: p.root })]);

  if (!steps.length) { log.err(`Unknown target: ${target}`); process.exit(1); }

  for (const [name, exec] of steps) {
    console.log('');
    log.info(`Building ${name}...`);
    console.log('─'.repeat(40));
    const code = exec();
    if (code !== 0) { log.err(`${name} failed (${code})`); process.exit(code); }
    log.ok(`${name}`);
  }
  console.log('');
  log.ok('All builds complete!');
}

function cmdNew(name: string): void {
  if (!name || !/^[a-z][a-z0-9_-]*$/.test(name)) {
    log.err('Service name: lowercase letters, digits, - or _');
    process.exit(1);
  }
  const p = loadPaths();
  const pascal = name.split(/[-_]/).map(s => s[0].toUpperCase() + s.slice(1)).join('');
  const dir = path.join(p.server_src_dir, 'skynet', name);

  if (fs.existsSync(dir)) { log.err(`Already exists: ${dir}`); process.exit(1); }
  fs.mkdirSync(dir, { recursive: true });

  fs.writeFileSync(path.join(dir, 'logic.ts'),
`import { IPlatform } from "../types";

export class ${pascal}Logic {
    private platform: IPlatform;

    constructor(platform: IPlatform) {
        this.platform = platform;
    }

    // TODO: 添加业务逻辑方法
}
`);

  fs.writeFileSync(path.join(dir, 'service.ts'),
`import { defineService } from "../service";
import { platform } from "../platform";
import { ${pascal}Logic } from "./logic";

const logic = new ${pascal}Logic(platform);

defineService({
    // TODO: 添加命令处理
}, function() {
    platform.log("info", "${name}_service started");
});
`);

  log.ok(`Created service: ${name}/`);
  console.log(`  ${path.join(dir, 'logic.ts')}`);
  console.log(`  ${path.join(dir, 'service.ts')}`);
  console.log('');
  log.dim('新服务已自动包含在 tstl 编译范围内 (server/src/skynet/**/*)');
  log.dim(`如需 Node.js 测试，在 tsconfig.node.json 中添加 "server/src/skynet/${name}/**/*"`);
}

function cmdTest(args: string[]): void {
  const p = loadPaths();
  const code = run('npx', ['jest', ...args], { cwd: p.root });
  process.exit(code);
}

function cmdClean(target: string): void {
  const p = loadPaths();

  if (target === 'proto' || target === 'all') {
    rmDir('proto desc', p.proto.desc_dir);
    rmDir('proto ts',   p.proto.ts_dir);
  }
  if (target === 'tables' || target === 'all') {
    rmDir('tables lua code', p.tables.lua_code_dir);
    rmDir('tables lua data', p.tables.lua_data_dir);
    rmDir('tables json data', p.tables.json_data_dir);
  }
  if (target === 'server' || target === 'all') {
    rmDir('server lua', p.skynet_lua_dir);
  }
  if (!['proto', 'tables', 'server', 'all'].includes(target)) {
    log.err(`Unknown target: ${target}`);
    log.dim('Usage: tslua clean [proto|tables|server|all]');
    process.exit(1);
  }
}

function cmdStatus(): void {
  const p = loadPaths();
  const svcDir = path.join(p.server_src_dir, 'skynet');

  const descN   = countFiles(p.proto.desc_dir, '.desc');
  const protoTsN = countFiles(p.proto.ts_dir, '.ts');
  const luaTblN = countFiles(p.tables.lua_data_dir, '.lua');
  const jsonTblN = countFiles(p.tables.json_data_dir, '.json');
  const luaSrvN = fs.existsSync(p.skynet_lua_dir)
    ? fs.readdirSync(p.skynet_lua_dir).filter(e => e.endsWith('.lua')).length : 0;

  const services: string[] = [];
  if (fs.existsSync(svcDir)) {
    for (const e of fs.readdirSync(svcDir, { withFileTypes: true })) {
      if (!e.isDirectory()) continue;
      if (fs.existsSync(path.join(svcDir, e.name, 'logic.ts')) &&
          fs.existsSync(path.join(svcDir, e.name, 'service.ts')))
        services.push(e.name);
    }
  }

  const tag = (ok: boolean) => ok ? `${C.green}Built${C.reset}` : `${C.yellow}--${C.reset}`;

  console.log('');
  console.log(`  ${C.bold}tslua2 project status${C.reset}`);
  console.log('  ═════════════════════');
  console.log(`  Proto:   ${tag(descN > 0)}  (${descN} desc, ${protoTsN} .ts)`);
  console.log(`  Tables:  ${tag(luaTblN > 0)}  (${luaTblN} Lua, ${jsonTblN} JSON)`);
  console.log(`  Server:  ${tag(luaSrvN > 0)}  (${luaSrvN} Lua files)`);
  if (services.length) {
    console.log('');
    console.log('  Services:');
    for (const s of services)
      console.log(`    ${C.cyan}${s}${C.reset}/  ${fs.readdirSync(path.join(svcDir, s)).join(', ')}`);
  }
  console.log('');
}

// ═══════════════════════════════════════════════════════════════════
// Main
// ═══════════════════════════════════════════════════════════════════
const args = process.argv.slice(2);
const cmd = args[0];
const sub = args[1];

switch (cmd) {
  case 'build':  cmdBuild(sub || 'all'); break;
  case 'new':    cmdNew(sub); break;
  case 'test':   cmdTest(args.slice(1)); break;
  case 'clean':  cmdClean(sub || 'all'); break;
  case 'status': cmdStatus(); break;
  default:
    console.log('');
    console.log(`  ${C.bold}tslua${C.reset} - 游戏服务端构建工具`);
    console.log('');
    console.log('  Commands:');
    console.log('    build [proto|tables|server|all]   构建项目');
    console.log('    new <name>                         创建新服务');
    console.log('    test [args...]                     运行测试');
    console.log('    clean [proto|tables|server|all]    清理输出');
    console.log('    status                             查看状态');
    console.log('');
    process.exit(cmd ? 1 : 0);
}
