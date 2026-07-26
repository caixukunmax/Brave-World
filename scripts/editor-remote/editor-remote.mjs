#!/usr/bin/env node
/**
 * Editor Remote CLI — 命令行调用 Godot Editor Remote 插件的 HTTP API
 *
 * 用法:
 *   node editor-remote.mjs info
 *   node editor-remote.mjs scene tree
 *   node editor-remote.mjs scene current
 *   node editor-remote.mjs scene open res://path/to/scene.tscn
 *   node editor-remote.mjs scene save
 *   node editor-remote.mjs node add <parent_path> <type> <name>
 *   node editor-remote.mjs node remove <node_path>
 *   node editor-remote.mjs node props <node_path> [prop1,prop2]
 *   node editor-remote.mjs node set <node_path> <prop_name> <json_value>
 *   node editor-remote.mjs node list <node_path>
 *   node editor-remote.mjs play start
 *   node editor-remote.mjs play stop
 *   node editor-remote.mjs play start_scene res://path/to/scene.tscn
 *   node editor-remote.mjs play screenshot [output.png]
 *   node editor-remote.mjs console [limit]
 *   node editor-remote.mjs console clear
 *   node editor-remote.mjs fs list [path]
 *   node editor-remote.mjs fs read <path>
 *   node editor-remote.mjs fs write <path> <content_file>
 *   node editor-remote.mjs resource load <path>
 *   node editor-remote.mjs resource save <path>
 *
 * 环境变量:
 *   EDITOR_REMOTE_PORT — 端口号，默认 7788
 */

const BASE_URL = `http://localhost:${process.env.EDITOR_REMOTE_PORT || 7788}`;

async function request(method, path, body) {
  const url = BASE_URL + path;
  const opts = {
    method,
    headers: { 'Content-Type': 'application/json' },
  };
  if (body !== undefined) {
    opts.body = JSON.stringify(body);
  }

  try {
    const res = await fetch(url, opts);
    const text = await res.text();
    let data;
    try { data = JSON.parse(text); } catch { data = text; }
    return { status: res.status, data };
  } catch (err) {
    console.error(`❌ 连接失败: ${err.message}`);
    console.error(`   请确认 Godot 编辑器已启动且 Editor Remote 插件已启用。`);
    console.error(`   地址: ${BASE_URL}`);
    process.exit(1);
  }
}

function printJson(obj) {
  console.log(JSON.stringify(obj, null, 2));
}

async function main() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    printUsage();
    return;
  }

  const [cmd, sub, ...rest] = args;

  switch (cmd) {
    case 'info': {
      const { data } = await request('GET', '/api/editor/info');
      printJson(data);
      break;
    }

    case 'scene': {
      switch (sub) {
        case 'tree': {
          const { data } = await request('GET', '/api/scene/tree');
          printJson(data);
          break;
        }
        case 'current': {
          const { data } = await request('GET', '/api/scene/current');
          printJson(data);
          break;
        }
        case 'open': {
          const path = rest[0];
          const { data } = await request('GET', `/api/scene/open?path=${encodeURIComponent(path)}`);
          printJson(data);
          break;
        }
        case 'save': {
          const { data } = await request('POST', '/api/scene/save');
          printJson(data);
          break;
        }
        case 'new': {
          const { data } = await request('POST', '/api/scene/new');
          printJson(data);
          break;
        }
        default:
          console.error('未知 scene 子命令: ' + sub);
          process.exit(1);
      }
      break;
    }

    case 'node': {
      switch (sub) {
        case 'add': {
          const [parentPath, type, name] = rest;
          const { data } = await request('POST', '/api/scene/node/add', {
            parent_path: parentPath, type, name
          });
          printJson(data);
          break;
        }
        case 'remove': {
          const [path] = rest;
          const { data } = await request('DELETE', `/api/scene/node/remove?path=${encodeURIComponent(path)}`);
          printJson(data);
          break;
        }
        case 'props': {
          const [path, names] = rest;
          const q = names ? `&names=${encodeURIComponent(names)}` : '';
          const { data } = await request('GET', `/api/node/properties?path=${encodeURIComponent(path)}${q}`);
          printJson(data);
          break;
        }
        case 'set': {
          const [path, propName, valueJson] = rest;
          let value;
          try { value = JSON.parse(valueJson); } catch { value = valueJson; }
          const { data } = await request('PUT', '/api/node/property', {
            path, name: propName, value
          });
          printJson(data);
          break;
        }
        case 'list': {
          const [path] = rest;
          const { data } = await request('GET', `/api/node/property_list?path=${encodeURIComponent(path)}`);
          printJson(data);
          break;
        }
        case 'rename': {
          const [path, name] = rest;
          const { data } = await request('POST', '/api/scene/node/rename', { path, name });
          printJson(data);
          break;
        }
        default:
          console.error('未知 node 子命令: ' + sub);
          process.exit(1);
      }
      break;
    }

    case 'play': {
      switch (sub) {
        case 'start': {
          const { data } = await request('POST', '/api/play/start');
          printJson(data);
          break;
        }
        case 'stop': {
          const { data } = await request('POST', '/api/play/stop');
          printJson(data);
          break;
        }
        case 'start_scene': {
          const [scene] = rest;
          const { data } = await request('POST', '/api/play/start_custom', { scene });
          printJson(data);
          break;
        }
        case 'status': {
          const { data } = await request('GET', '/api/play/status');
          printJson(data);
          break;
        }
        case 'screenshot': {
          const [outFile] = rest;
          const { data } = await request('GET', '/api/play/screenshot');
          if (data?.data?.data_base64) {
            const buf = Buffer.from(data.data.data_base64, 'base64');
            const fname = outFile || `screenshot_${Date.now()}.png`;
            require('fs').writeFileSync(fname, buf);
            console.log(`✅ 截图已保存: ${fname} (${data.data.width}x${data.data.height})`);
          } else {
            printJson(data);
          }
          break;
        }
        default:
          console.error('未知 play 子命令: ' + sub);
          process.exit(1);
      }
      break;
    }

    case 'console': {
      if (sub === 'clear') {
        const { data } = await request('DELETE', '/api/console');
        printJson(data);
      } else {
        const limit = sub || '100';
        const { data } = await request('GET', `/api/console?limit=${limit}`);
        if (data?.data?.lines) {
          data.data.lines.forEach(l => console.log(l));
        } else {
          printJson(data);
        }
      }
      break;
    }

    case 'fs': {
      switch (sub) {
        case 'list': {
          const path = rest[0] || 'res://';
          const { data } = await request('GET', `/api/filesystem/list?path=${encodeURIComponent(path)}`);
          printJson(data);
          break;
        }
        case 'read': {
          const [path] = rest;
          const { data } = await request('GET', `/api/filesystem/read?path=${encodeURIComponent(path)}`);
          if (data?.data?.content) {
            console.log(data.data.content);
          } else {
            printJson(data);
          }
          break;
        }
        case 'write': {
          const [path, contentFile] = rest;
          const fs = require('fs');
          const content = fs.readFileSync(contentFile, 'utf-8');
          const { data } = await request('POST', '/api/filesystem/write', { path, content });
          printJson(data);
          break;
        }
        default:
          console.error('未知 fs 子命令: ' + sub);
          process.exit(1);
      }
      break;
    }

    case 'resource': {
      switch (sub) {
        case 'load': {
          const [path] = rest;
          const { data } = await request('GET', `/api/resource/load?path=${encodeURIComponent(path)}`);
          printJson(data);
          break;
        }
        case 'save': {
          const [path] = rest;
          const { data } = await request('POST', '/api/resource/save', { path });
          printJson(data);
          break;
        }
        default:
          console.error('未知 resource 子命令: ' + sub);
          process.exit(1);
      }
      break;
    }

    default:
      console.error('未知命令: ' + cmd);
      printUsage();
      process.exit(1);
  }
}

function printUsage() {
  console.log(`
Editor Remote CLI — 远程控制 Godot 编辑器

用法:
  node editor-remote.mjs <命令> [子命令] [参数...]

常用命令:
  info                        编辑器信息 + API 列表
  scene tree                  当前场景节点树
  scene current               当前场景信息
  scene open <path>           打开场景
  scene save                  保存场景
  node add <parent> <type> <name>   添加节点
  node remove <path>          删除节点
  node props <path> [names]   读取节点属性
  node set <path> <prop> <val> 设置节点属性
  node list <path>            节点属性定义列表
  play start                  运行当前场景
  play stop                   停止运行
  play screenshot [out.png]   截图保存到文件
  console [limit]             查看控制台输出
  console clear               清空控制台
  fs list [path]              列出目录
  fs read <path>              读文本文件
  fs write <path> <file>      写文本文件（内容来自文件）
  resource load <path>        加载资源信息
  resource save <path>        保存资源

环境变量:
  EDITOR_REMOTE_PORT  端口号 (默认 7788)
`);
}

main().catch(console.error);
