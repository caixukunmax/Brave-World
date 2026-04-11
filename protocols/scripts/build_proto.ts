#!/usr/bin/env tsx
/**
 * =============================================================================
 * Protocol Buffers 编译脚本
 * 功能：编译 .proto 文件生成 Lua 描述文件和 TypeScript 代码
 * 特点：跨平台支持（Windows/Linux/Mac），使用 ts-proto 生成纯 TS 类型
 * =============================================================================
 */

import path from 'path';
import fs from 'fs';
import { execSync } from 'child_process';
import { globSync } from 'glob';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);

// =============================================================================
// 类型定义
// =============================================================================
interface ProtoConfig {
  proto_dirs: string[];
  skip_ts_generation?: boolean;
}

interface PathsConfig {
  proto: {
    desc_dir: string;
    lua_enum_dir: string;
    ts_dir: string;
    cs_dir: string;
  };
}

// =============================================================================
// 工具函数
// =============================================================================
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

// =============================================================================
// 主逻辑
// =============================================================================
function main(): void {
  const scriptDir = path.dirname(__filename);
  const baseDir = path.resolve(scriptDir, '..');

  // 加载配置文件
  const configPath = path.join(baseDir, 'proto.config.json');
  if (!fs.existsSync(configPath)) {
    error(`Config file not found: ${configPath}`);
    process.exit(1);
  }

  const config: ProtoConfig = JSON.parse(fs.readFileSync(configPath, 'utf-8'));
  info(`Loaded config: ${configPath}`);

  // 加载路径配置（paths.json 在项目根目录，路径相对于根目录）
  const rootDir = path.resolve(baseDir, '..');
  const pathsPath = path.join(rootDir, 'paths.json');
  if (!fs.existsSync(pathsPath)) {
    error(`Paths config not found: ${pathsPath}`);
    process.exit(1);
  }
  const paths: PathsConfig = JSON.parse(fs.readFileSync(pathsPath, 'utf-8'));
  const outputLuaDirs = [path.resolve(rootDir, paths.proto.desc_dir)];
  const luaEnumDirs = [path.resolve(rootDir, paths.proto.lua_enum_dir)];
  const outputTsDirs = [path.resolve(rootDir, paths.proto.ts_dir)];
  const outputCsDir = path.resolve(rootDir, paths.proto.cs_dir);

  console.log('');
  console.log('========================================');
  console.log('  Compiling Protocol Buffers');
  console.log('========================================');
  console.log('');

  // 收集所有 proto 文件
  const allProtoFiles: string[] = [];
  for (const protoDir of config.proto_dirs) {
    const fullDir = path.resolve(baseDir, protoDir);
    if (!fs.existsSync(fullDir)) {
      warn(`Proto directory not found: ${fullDir}`);
      continue;
    }
    info(`Scanning proto directory: ${protoDir}`);
    const protoFiles = globSync('**/*.proto', { cwd: fullDir }).sort();
    for (const file of protoFiles) {
      allProtoFiles.push(path.join(fullDir, file));
    }
  }

  if (allProtoFiles.length === 0) {
    error('No .proto files found');
    process.exit(1);
  }

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

  // 生成 Lua 描述文件
  if (protocCmd) {
    console.log('----------------------------------------');
    info('Generating Lua descriptor files...');
    console.log('----------------------------------------');

    for (const luaDir of outputLuaDirs) {
      const fullLuaDir = path.resolve(baseDir, luaDir);
      fs.mkdirSync(fullLuaDir, { recursive: true });
      info(`Output directory: ${luaDir}`);

      for (const protoFile of allProtoFiles) {
        const filename = path.basename(protoFile, '.proto');
        const protoPath = path.dirname(protoFile);
        const outFile = path.join(fullLuaDir, `${filename}_pb.desc`);

        try {
          execSync(
            `"${protocCmd}" --proto_path="${protoPath}" --descriptor_set_out="${outFile}" --include_imports "${protoFile}"`,
            { stdio: 'ignore' }
          );
          success(`${luaDir}/${filename}.desc`);
        } catch {
          warn(`${luaDir}/${filename}.desc (failed)`);
        }
      }
    }
    console.log('');
  }

  // 生成 Lua 枚举常量文件（直接导出为 .lua，方便 IDE 跳转和类型查看）
  console.log('');
  console.log('----------------------------------------');
  info('Generating Lua enum files...');
  console.log('----------------------------------------');

  for (const enumDir of luaEnumDirs) {
    fs.mkdirSync(enumDir, { recursive: true });
  }

  for (const protoFile of allProtoFiles) {
    const filename = path.basename(protoFile, '.proto');
    const content = fs.readFileSync(protoFile, 'utf-8');
    const enums = parseProtoEnums(content);
    if (enums.length === 0) continue;

    const luaContent = generateLuaEnumFile(filename, enums);
    for (const enumDir of luaEnumDirs) {
      fs.writeFileSync(path.join(enumDir, `${filename}_enum.lua`), luaContent);
    }
    success(`${filename}_enum.lua`);
  }

  // 生成 Lua 消息编解码文件（方便 IDE 跳转，避免手写字符串）
  console.log('');
  console.log('----------------------------------------');
  info('Generating Lua proto files...');
  console.log('----------------------------------------');

  for (const protoFile of allProtoFiles) {
    const filename = path.basename(protoFile, '.proto');
    const content = fs.readFileSync(protoFile, 'utf-8');
    const messages = parseProtoMessages(content);
    if (messages.length === 0) continue;

    const packageName = parseProtoPackage(content);
    const luaContent = generateLuaProtoFile(filename, packageName, messages);
    for (const enumDir of luaEnumDirs) {
      fs.writeFileSync(path.join(enumDir, `${filename}_proto.lua`), luaContent);
    }
    success(`${filename}_proto.lua`);
  }

  // 生成 index.lua 统一导出（枚举 + 消息）
  const luaIndexContent = generateLuaIndex(allProtoFiles, luaEnumDirs[0]);
  if (luaIndexContent) {
    for (const enumDir of luaEnumDirs) {
      fs.writeFileSync(path.join(enumDir, 'index.lua'), luaIndexContent);
    }
    success('index.lua (enum index)');
  }

  // 生成 msg_id_map.lua（msg_id → 名称 + protobuf 类型）
  const msgIdMapContent = generateMsgIdMap(allProtoFiles);
  if (msgIdMapContent) {
    for (const enumDir of luaEnumDirs) {
      fs.writeFileSync(path.join(enumDir, 'msg_id_map.lua'), msgIdMapContent);
    }
    success('msg_id_map.lua');
  }

  // 生成 TypeScript 代码 (使用 ts-proto)
  const firstTsDir = path.resolve(baseDir, outputTsDirs[0]);
  if (config.skip_ts_generation) {
    info('Skipping TypeScript generation (manual mode)');
    console.log('');
  } else {
  console.log('----------------------------------------');
  info('Generating TypeScript files with ts-proto...');
  console.log('----------------------------------------');

  for (const tsDir of outputTsDirs) {
    const fullTsDir = path.resolve(baseDir, tsDir);
    fs.mkdirSync(fullTsDir, { recursive: true });
    info(`Output directory: ${tsDir}`);
  }

  if (!protocCmd) {
    error('protoc not found');
    process.exit(1);
  }

  // 查找 ts-proto 插件（优先本地，其次根目录）
  const projectRoot = path.resolve(baseDir, '..');
  const localPlugin = path.join(baseDir, 'node_modules', '.bin', 'protoc-gen-ts_proto.cmd');
  const localPluginAlt = path.join(baseDir, 'node_modules', '.bin', 'protoc-gen-ts_proto');
  const rootPlugin = path.join(projectRoot, 'node_modules', '.bin', 'protoc-gen-ts_proto.cmd');
  const rootPluginAlt = path.join(projectRoot, 'node_modules', '.bin', 'protoc-gen-ts_proto');

  let pluginPath: string;
  if (fs.existsSync(localPlugin)) {
    pluginPath = localPlugin;
  } else if (fs.existsSync(localPluginAlt)) {
    pluginPath = localPluginAlt;
  } else if (fs.existsSync(rootPlugin)) {
    pluginPath = rootPlugin;
  } else if (fs.existsSync(rootPluginAlt)) {
    pluginPath = rootPluginAlt;
  } else {
    error('ts-proto plugin not found. Run: npm install ts-proto');
    process.exit(1);
  }

  info(`Using protoc: ${protocCmd}`);
  info(`Using ts-proto plugin: ${pluginPath}`);

  // 为每个 proto 文件生成 TypeScript
  for (const protoFile of allProtoFiles) {
    const filename = path.basename(protoFile, '.proto');
    const protoPath = path.dirname(protoFile);

    try {
      const args = [
        `--plugin=protoc-gen-ts_proto=${pluginPath}`,
        `--ts_proto_out=${firstTsDir}`,
        `--ts_proto_opt=outputServices=false,onlyTypes=true,useExactTypes=false,stringEnums=false,useOptionals=messages,snakeToCamel=false`,
        `--proto_path=${protoPath}`,
        protoFile
      ];

      execSync(`"${protocCmd}" ${args.join(' ')}`, { stdio: 'pipe' });
      success(`${filename}.ts`);
    } catch (err) {
      warn(`${filename}.ts (failed)`);
      if (err instanceof Error) {
        console.error(err.message);
      }
    }
  }

  }

  // 生成 index.ts 导出文件
  if (!config.skip_ts_generation) {
    const indexTsPath = path.join(firstTsDir, 'index.ts');
    const indexContent = generateIndexTs(firstTsDir);
    fs.writeFileSync(indexTsPath, indexContent);
    success('index.ts (generated)');
  }

  // 生成 C# 代码 (Google.Protobuf)
  if (protocCmd && outputCsDir) {
    console.log('');
    console.log('----------------------------------------');
    info('Generating C# files with protoc --csharp_out...');
    console.log('----------------------------------------');

    fs.mkdirSync(outputCsDir, { recursive: true });
    info(`Output directory: ${outputCsDir}`);

    // 清空旧文件
    const oldCsFiles = fs.readdirSync(outputCsDir).filter(f => f.endsWith('.cs'));
    for (const f of oldCsFiles) {
      fs.unlinkSync(path.join(outputCsDir, f));
    }

    for (const protoFile of allProtoFiles) {
      const filename = path.basename(protoFile, '.proto');
      const protoPath = path.dirname(protoFile);

      try {
        execSync(
          `"${protocCmd}" --proto_path="${protoPath}" --csharp_out="${outputCsDir}" "${protoFile}"`,
          { stdio: 'pipe' }
        );
        success(`${filename}.cs`);
      } catch (err) {
        warn(`${filename}.cs (failed)`);
        if (err instanceof Error) {
          console.error(err.message);
        }
      }
    }
  }

  console.log('');
  console.log('========================================');
  success('Protocol compilation complete!');
  console.log('========================================');
  console.log('');
}

// 生成 index.ts 内容（自动解析类型并生成 create 辅助函数）
function generateIndexTs(protosDir: string): string {

  // 解析生成的 .ts 文件，提取类型信息
  const typeInfos = parseGeneratedTypes(protosDir);

  // 生成导出语句
  const exportLines: string[] = [];
  const importLines: string[] = [];

  // 收集所有模块
  const modules = new Map<string, string[]>();
  for (const info of typeInfos) {
    if (!modules.has(info.module)) {
      modules.set(info.module, []);
    }
    modules.get(info.module)!.push(info.name);
  }

  // 生成导出
  for (const [module, names] of modules) {
    const enums = names.filter(n => typeInfos.find(t => t.name === n && t.isEnum));
    const types = names.filter(n => typeInfos.find(t => t.name === n && !t.isEnum));

    if (enums.length > 0) {
      exportLines.push(`export { ${enums.join(', ')} } from './${module}';`);
    }
    if (types.length > 0) {
      exportLines.push(`export type { ${types.join(', ')} } from './${module}';`);
    }
  }

  // 生成导入（用于 create 方法）
  for (const [module, names] of modules) {
    const enums = names.filter(n => typeInfos.find(t => t.name === n && t.isEnum));
    const types = names.filter(n => typeInfos.find(t => t.name === n && !t.isEnum));
    if (enums.length > 0) {
      importLines.push(`import { ${enums.join(', ')} } from './${module}';`);
    }
    if (types.length > 0) {
      importLines.push(`import type { ${types.join(', ')} } from './${module}';`);
    }
  }

  // 生成 proto 对象
  const protoLines: string[] = [
    '// 通用 create 辅助函数',
    'function createMessage<T>(defaults: Partial<T>, init?: Partial<T>): T {',
    '  return { ...defaults, ...init } as T;',
    '}',
    '',
    '// 创建 proto 对象（自动生成）',
    'export const proto = {',
  ];

  // 按模块分组生成 create / decode / encode 方法
  for (const [module, names] of modules) {
    const moduleTypes = typeInfos.filter(t => t.module === module);
    protoLines.push(`  ${module}: {`);

    for (const name of names) {
      const info = moduleTypes.find(t => t.name === name);
      if (!info) continue;

      if (info.isEnum) {
        // 枚举直接引用
        protoLines.push(`    ${name},`);
      } else {
        // 生成 create / decode / encode 方法
        const defaults = generateDefaults(info);
        const fullName = `${module}.${name}`;
        protoLines.push(`    ${name}: {`);
        protoLines.push(`      create: (init?: Partial<${name}>): ${name} =>`);
        protoLines.push(`        createMessage(${defaults}, init),`);
        protoLines.push(`      decode: (data: string): ${name} =>`);
        protoLines.push(`        pb_decode("${fullName}", data) as ${name},`);
        protoLines.push(`      encode: (msg: ${name}): string =>`);
        protoLines.push(`        pb_encode("${fullName}", msg),`);
        protoLines.push(`    },`);
      }
    }
    protoLines.push('  },');
  }

  protoLines.push('};');
  protoLines.push('');
  protoLines.push('export default proto;');

  return `/**
 * Protocol Buffers TypeScript 定义
 * 由 ts-proto 自动生成
 * 源文件: protocols/proto/*.proto
 * 生成命令: npm run build:proto
 */

${exportLines.join('\n')}

${importLines.join('\n')}

${protoLines.join('\n')}
`;
}

// 解析生成的类型文件
function parseGeneratedTypes(protosDir: string): TypeInfo[] {
  const types: TypeInfo[] = [];

  const files = fs.readdirSync(protosDir).filter(f => f.endsWith('.ts') && f !== 'index.ts');

  for (const file of files) {
    const module = path.basename(file, '.ts');
    const content = fs.readFileSync(path.join(protosDir, file), 'utf-8');

    // 解析枚举
    const enumRegex = /export enum (\w+)\s*\{([^}]+)\}/g;
    let match;
    while ((match = enumRegex.exec(content)) !== null) {
      types.push({
        module,
        name: match[1],
        isEnum: true,
        isEnumType: false,
        fields: [],
      });
    }

    // 解析接口
    const interfaceRegex = /export interface (\w+)\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}/g;
    while ((match = interfaceRegex.exec(content)) !== null) {
      const name = match[1];
      const body = match[2];
      const fields = parseFields(body);

      types.push({
        module,
        name,
        isEnum: false,
        isEnumType: fields.some(f => f.type.includes('ErrorCode')),
        fields,
      });
    }
  }

  return types;
}

// 解析字段
function parseFields(body: string): FieldInfo[] {
  const fields: FieldInfo[] = [];
  const lines = body.split('\n').map(l => l.trim()).filter(l => l && !l.startsWith('*') && !l.startsWith('//'));

  for (const line of lines) {
    // 匹配: fieldName?: Type; 或 fieldName: Type;
    const fieldMatch = line.match(/^(\w+)\??:\s*(.+?);?$/);
    if (fieldMatch) {
      const name = fieldMatch[1];
      const type = fieldMatch[2].replace(/;$/, '').trim();
      const isOptional = line.includes('?');
      fields.push({ name, type, isOptional });
    }
  }

  return fields;
}

// 生成默认值
function generateDefaults(info: TypeInfo): string {
  const defaults: string[] = [];

  for (const field of info.fields) {
    const defaultVal = getDefaultValue(field.type, field.isOptional);
    defaults.push(`${field.name}: ${defaultVal}`);
  }

  return `{ ${defaults.join(', ')} }`;
}

// 获取类型的默认值
function getDefaultValue(type: string, isOptional: boolean): string {
  if (isOptional) return 'undefined';

  // 移除数组标记和联合类型
  const baseType = type.replace(/\[\]/g, '').replace(/\|\s*undefined/g, '').replace(/\|\s*null/g, '').trim();

  if (baseType === 'string') return "''";
  if (baseType === 'number' || baseType === 'long') return '0';
  if (baseType === 'boolean') return 'false';
  if (baseType === 'Uint8Array') return 'new Uint8Array(0)';
  if (baseType === 'ErrorCode') return 'ErrorCode.SUCCESS';

  // 数组类型
  if (type.includes('[]')) return '[]';

  // 嵌套类型
  return 'undefined';
}

interface TypeInfo {
  module: string;
  name: string;
  isEnum: boolean;
  isEnumType: boolean;  // 是否包含 ErrorCode 字段
  fields: FieldInfo[];
}

interface FieldInfo {
  name: string;
  type: string;
  isOptional: boolean;
}

// =============================================================================
// msg_id_map 生成（msg_id → 名称 + protobuf 类型）
// =============================================================================

/**
 * 从 MessageId 枚举名推导 protobuf 类型
 * LOGIN_ACCOUNT_LOGIN_REQ → login.AccountLoginRequest
 * GAME_CREATE_ROLE_RSP   → game.CreateRoleResponse
 */
function enumNameToProtoType(name: string): string | null {
  const suffixes: Record<string, string> = { REQ: 'Request', RSP: 'Response', NOTIFY: 'Notify' };
  const parts = name.split('_');
  const direction = parts[parts.length - 1];

  if (!suffixes[direction]) return null;

  const module = parts[0].toLowerCase();
  const messageBase = parts.slice(1, -1)
    .map(p => p.charAt(0).toUpperCase() + p.slice(1).toLowerCase())
    .join('');

  return `${module}.${messageBase}${suffixes[direction]}`;
}

/**
 * 生成 msg_id_map.lua
 */
function generateMsgIdMap(protoFiles: string[]): string {
  // 找 message_id.proto
  const msgIdFile = protoFiles.find(f => path.basename(f) === 'message_id.proto');
  if (!msgIdFile) return '';

  const content = fs.readFileSync(msgIdFile, 'utf-8');
  const enums = parseProtoEnums(content);
  const messageIdEnum = enums.find(e => e.name === 'MessageId');
  if (!messageIdEnum) return '';

  const nameLines: string[] = [];
  const typeLines: string[] = [];

  for (const entry of messageIdEnum.entries) {
    if (entry.value === 0) continue; // skip UNKNOWN

    nameLines.push(`    [${entry.value}] = "${entry.name}",`);

    const protoType = enumNameToProtoType(entry.name);
    if (protoType) {
      typeLines.push(`    [${entry.value}] = "${protoType}",`);
    }
  }

  return `--------------------------------------------------------------------------------
-- msg_id 映射表（由 build_proto 自动生成，请勿手动修改）
-- Source: message_id.proto
--------------------------------------------------------------------------------
local M = {}

-- msg_id → 枚举名称
M.name = {
${nameLines.join('\n')}
}

-- msg_id → protobuf 类型（用于解码包体内容）
M.type = {
${typeLines.join('\n')}
}

return M
`;
}

// =============================================================================
// Lua 枚举生成
// =============================================================================

interface EnumEntry {
  name: string;
  value: number;
  comment: string;
}

interface EnumDef {
  name: string;
  entries: EnumEntry[];
  comment: string;
}

/**
 * 解析 .proto 文件中的 enum 定义
 */
function parseProtoEnums(content: string): EnumDef[] {
  const enums: EnumDef[] = [];

  // 匹配 enum 块（含可选注释）
  const enumRegex = /(?:\/\*\*[\s\S]*?\*\/\s*)?(?:\/\/[^\n]*\n\s*)*enum\s+(\w+)\s*\{([^}]*)\}/g;
  let match;

  while ((match = enumRegex.exec(content)) !== null) {
    const enumName = match[1];
    const body = match[2];
    const entries: EnumEntry[] = [];

    // 匹配每个枚举项: NAME = VALUE; // comment
    const entryRegex = /^\s*(\w+)\s*=\s*(\d+)\s*;\s*(?:\/\/\s*(.*))?$/gm;
    let entryMatch;
    while ((entryMatch = entryRegex.exec(body)) !== null) {
      entries.push({
        name: entryMatch[1],
        value: parseInt(entryMatch[2], 10),
        comment: (entryMatch[3] || '').trim(),
      });
    }

    if (entries.length > 0) {
      // 提取 enum 上方的注释
      const beforeEnum = content.substring(0, match.index);
      const lastCommentMatch = beforeEnum.match(/\/\*\*[\s\S]*?\*\/\s*$/);
      const enumComment = lastCommentMatch
        ? lastCommentMatch[0].replace(/\/\*\*|\*\//g, '').replace(/\s*\*\s?/g, ' ').trim()
        : '';

      enums.push({ name: enumName, entries, comment: enumComment });
    }
  }

  return enums;
}

/**
 * 生成单个 proto 文件的 Lua 枚举文件内容
 */
function generateLuaEnumFile(protoName: string, enums: EnumDef[]): string {
  const lines: string[] = [];

  lines.push('--------------------------------------------------------------------------------');
  lines.push(`-- 枚举常量（由 build_proto 自动生成，请勿手动修改）`);
  lines.push(`-- Source: ${protoName}.proto`);
  lines.push('--------------------------------------------------------------------------------');
  lines.push('');

  for (const e of enums) {
    if (e.comment) {
      lines.push(`-- ${e.comment}`);
    }
    lines.push(`local ${e.name} = {`);

    for (const entry of e.entries) {
      const comment = entry.comment ? `  -- ${entry.comment}` : '';
      lines.push(`    ${entry.name} = ${entry.value},${comment}`);
    }

    lines.push('}');
    lines.push('');
  }

  // 返回表
  lines.push('return {');
  for (const e of enums) {
    lines.push(`    ${e.name} = ${e.name},`);
  }
  lines.push('}');

  return lines.join('\n') + '\n';
}

/**
 * 生成 index.lua，统一导出所有枚举和消息编解码
 */
function generateLuaIndex(protoFiles: string[], enumDir: string): string {
  const enumModules: string[] = [];
  const protoModules: string[] = [];

  for (const protoFile of protoFiles) {
    const filename = path.basename(protoFile, '.proto');
    const content = fs.readFileSync(protoFile, 'utf-8');

    const enums = parseProtoEnums(content);
    if (enums.length > 0) enumModules.push(filename);

    const messages = parseProtoMessages(content);
    if (messages.length > 0) protoModules.push(filename);
  }

  if (enumModules.length === 0 && protoModules.length === 0) return '';

  const lines: string[] = [];
  lines.push('--------------------------------------------------------------------------------');
  lines.push('-- 协议索引（由 build_proto 自动生成，请勿手动修改）');
  lines.push('--------------------------------------------------------------------------------');
  lines.push('');

  for (const mod of enumModules) {
    lines.push(`local ${mod}_enum = require "protos.${mod}_enum"`);
  }
  for (const mod of protoModules) {
    lines.push(`local ${mod}_proto = require "protos.${mod}_proto"`);
  }
  lines.push('');

  lines.push('return {');

  for (const mod of enumModules) {
    const hasProto = protoModules.includes(mod);
    if (hasProto) {
      lines.push(`    ${mod} = setmetatable({}, {`);
      lines.push(`        __index = function(_, key)`);
      lines.push(`            local v = rawget(${mod}_enum, key) or rawget(${mod}_proto, key)`);
      lines.push(`            if v ~= nil then return v end`);
      lines.push(`        end,`);
      lines.push(`    }),`);
    } else {
      lines.push(`    ${mod} = ${mod}_enum,`);
    }
  }

  // 纯 proto 模块（没有 enum 的）
  for (const mod of protoModules) {
    if (enumModules.includes(mod)) continue;
    lines.push(`    ${mod} = ${mod}_proto,`);
  }

  lines.push('}');

  return lines.join('\n') + '\n';
}

// =============================================================================
// Lua 消息编解码生成
// =============================================================================

interface MsgField {
  name: string;
  type: string;
  repeated: boolean;
  comment: string;
}

interface MsgDef {
  name: string;
  fields: MsgField[];
  comment: string;
}

/**
 * 解析 .proto 文件中的 package 名称
 */
function parseProtoPackage(content: string): string {
  const match = content.match(/^\s*package\s+(\w+)\s*;/m);
  return match ? match[1] : '';
}

/**
 * 解析 .proto 文件中的 message 定义
 */
function parseProtoMessages(content: string): MsgDef[] {
  const messages: MsgDef[] = [];

  // 匹配 message 块
  const msgRegex = /(?:\/\*\*[\s\S]*?\*\/\s*)?(?:\/\/[^\n]*\n\s*)*message\s+(\w+)\s*\{([^}]*)\}/g;
  let match;

  while ((match = msgRegex.exec(content)) !== null) {
    const msgName = match[1];
    const body = match[2];
    const fields: MsgField[] = [];

    // 匹配字段: [repeated] type name = number; // comment
    const fieldRegex = /^\s*(repeated\s+)?([\w.]+)\s+(\w+)\s*=\s*\d+\s*;\s*(?:\/\/\s*(.*))?$/gm;
    let fieldMatch;
    while ((fieldMatch = fieldRegex.exec(body)) !== null) {
      fields.push({
        repeated: !!fieldMatch[1],
        type: fieldMatch[2],
        name: fieldMatch[3],
        comment: (fieldMatch[4] || '').trim(),
      });
    }

    if (fields.length > 0) {
      // 提取 message 上方的注释
      const beforeMsg = content.substring(0, match.index);
      const lastCommentMatch = beforeMsg.match(/\/\*\*[\s\S]*?\*\/\s*$/);
      const msgComment = lastCommentMatch
        ? lastCommentMatch[0].replace(/\/\*\*|\*\//g, '').replace(/\s*\*\s?/g, ' ').trim()
        : '';

      messages.push({ name: msgName, fields, comment: msgComment });
    }
  }

  return messages;
}

/**
 * 生成单个 proto 文件的 Lua 消息编解码文件
 */
function generateLuaProtoFile(protoName: string, packageName: string, messages: MsgDef[]): string {
  const lines: string[] = [];

  lines.push('--------------------------------------------------------------------------------');
  lines.push(`-- 消息编解码（由 build_proto 自动生成，请勿手动修改）`);
  lines.push(`-- Source: ${protoName}.proto  Package: ${packageName}`);
  lines.push('--------------------------------------------------------------------------------');
  lines.push('local pb = require "pb"');
  lines.push('');
  lines.push('local M = {}');
  lines.push('');

  for (const msg of messages) {
    // 字段注释行
    const fieldParts = msg.fields.map(f => {
      const prefix = f.repeated ? `${f.type}[]` : f.type;
      const comment = f.comment ? ` -- ${f.comment}` : '';
      return `${f.name}(${prefix})${comment}`;
    });

    if (msg.comment) {
      lines.push(`--- ${msg.comment}`);
    }
    lines.push(`-- Fields: ${fieldParts.join(' ')}`);
    lines.push(`M.${msg.name} = {`);
    lines.push(`    encode = function(data) return pb.encode("${packageName}.${msg.name}", data) end,`);
    lines.push(`    decode = function(data) return pb.decode("${packageName}.${msg.name}", data) end,`);
    lines.push('}');
    lines.push('');
  }

  lines.push('return M');

  return lines.join('\n') + '\n';
}

// 运行主函数
main();
