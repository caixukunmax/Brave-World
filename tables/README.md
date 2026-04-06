# Luban 配置表

本目录包含项目的所有 Luban 配置表定义和数据文件。

## 📁 目录结构

```
tables/
├── datas/                  # Excel 数据文件
│   ├── __tables__.xlsx     # 表定义
│   ├── __beans__.xlsx      # Bean 定义
│   ├── __enums__.xlsx      # 枚举定义
│   ├── common/             # 通用配置
│   ├── item/               # 道具系统
│   ├── role/               # 角色系统
│   └── ...
│
├── defines/                # XML 定义文件
│   ├── common.xml
│   ├── item.xml
│   ├── builtin.xml
│   └── ...
│
├── tools/
│   └── luban/              # Luban 工具
│       └── Luban/          # Luban.dll
├── luban.conf              # Luban 配置文件
├── luban.config.json       # 编译配置（输入输出路径）
├── package.json            # npm 包配置
├── tsconfig.json           # TypeScript 配置
└── scripts/
    └── build_tables.ts     # TypeScript 编译脚本
```

## 🔧 使用方法

### 独立运行（推荐）

```bash
# 进入 tables 目录
cd tables

# 安装依赖（如果需要）
npm install

# 编译配置表
npm run build
```

### 从根目录运行

```bash
# 在项目根目录
npm run build:tables
```

### 从 server 目录运行

```bash
cd server
npm run build:tables
```

编译后会生成：
- **TypeScript/Lua**: `../server/src/tables/`
- **JSON 数据**: `../server/src/tables/data/`

## 🛠️ 依赖要求

- **.NET SDK 8.0+**: Luban 需要 dotnet 运行时
- **Luban.dll**: 位于 `tables/tools/luban/Luban/`

### 安装 .NET SDK

```bash
# Ubuntu/Debian
sudo apt-get install dotnet-sdk-8.0

# macOS
brew install --cask dotnet-sdk

# Windows
# 访问 https://dotnet.microsoft.com/download
```

## 📝 配置说明

### 编译配置 (`luban.config.json`)

控制输入目录和输出目录：

```json
{
  "luban_dll": "tools/luban/Luban/Luban.dll",
  "input": {
    "data_dir": "datas",          // 数据源目录
    "define_dir": "defines",       // 定义文件目录
    "config_file": "luban.conf"    // Luban 配置文件
  },
  "output": {
    "code_dir": "../server/src/tables",         // 代码输出目录
    "data_dir": "../server/src/tables/data"     // 数据输出目录
  },
  "luban_args": {
    "l10n_text_provider_file": "datas/l10n/texts.json"
  }
}
```

### Luban 配置 (`luban.conf`)

```json
{
  "groups": [
    {"names":["c"], "default":true},
    {"names":["s"], "default":true},
    {"names":["e"], "default":true}
  ],
  "schemaFiles": [
    {"fileName":"defines", "type":""},
    {"fileName":"datas/__tables__.xlsx", "type":"table"},
    {"fileName":"datas/__beans__.xlsx", "type":"bean"},
    {"fileName":"datas/__enums__.xlsx", "type":"enum"}
  ],
  "dataDir": "datas",
  "targets": [
    {"name":"lua", "manager":"Tables", "groups":["c","s","e"], "topModule":"cfg"},
    {"name":"json", "manager":"Tables", "groups":["c","s","e"], "topModule":"cfg"}
  ]
}
```

## 🔄 添加新表

1. 在 `datas/` 目录创建新的 Excel 文件
2. 在 `defines/` 目录创建对应的 XML 定义（如果需要）
3. 更新 `__tables__.xlsx` 注册新表
4. 运行 `npm run build` 编译

## 📍 修改输出路径

如需修改生成文件的输出路径，编辑 `luban.config.json`：

```json
{
  "output": {
    "code_dir": "../其他目录/src/tables",
    "data_dir": "../其他目录/src/tables/data"
  }
}
```

## 📚 参考资料

- [Luban 官方文档](https://luban.doc.code-philosophy.com/)
- [Luban GitHub](https://github.com/focus-creative-games/luban)
