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

// 运行主函数
main();
