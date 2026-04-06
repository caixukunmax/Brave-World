local ____lualib = require("lualib_bundle")
local __TS__ArraySort = ____lualib.__TS__ArraySort
local Error = ____lualib.Error
local RangeError = ____lualib.RangeError
local ReferenceError = ____lualib.ReferenceError
local SyntaxError = ____lualib.SyntaxError
local TypeError = ____lualib.TypeError
local URIError = ____lualib.URIError
local __TS__InstanceOf = ____lualib.__TS__InstanceOf
local Map = ____lualib.Map
local __TS__New = ____lualib.__TS__New
local __TS__ArrayFind = ____lualib.__TS__ArrayFind
local __TS__ArrayFilter = ____lualib.__TS__ArrayFilter
local __TS__Iterator = ____lualib.__TS__Iterator
local __TS__StringEndsWith = ____lualib.__TS__StringEndsWith
local __TS__StringIncludes = ____lualib.__TS__StringIncludes
local __TS__ArraySome = ____lualib.__TS__ArraySome
local __TS__StringStartsWith = ____lualib.__TS__StringStartsWith
local __TS__StringTrim = ____lualib.__TS__StringTrim
local __TS__StringSplit = ____lualib.__TS__StringSplit
local __TS__ArrayMap = ____lualib.__TS__ArrayMap
local __TS__StringReplace = ____lualib.__TS__StringReplace
local ____exports = {}
local generateIndexTs, parseGeneratedTypes, parseFields, generateDefaults, getDefaultValue
local ____path = require("path")
local path = ____path.default
local ____fs = require("fs")
local fs = ____fs.default
local ____child_process = require("child_process")
local execSync = ____child_process.execSync
local ____glob = require("glob")
local globSync = ____glob.globSync
local ____url = require("url")
local fileURLToPath = ____url.fileURLToPath
function generateIndexTs(self, protosDir)
    local typeInfos = parseGeneratedTypes(nil, protosDir)
    local exportLines = {}
    local importLines = {}
    local modules = __TS__New(Map)
    for ____, info in ipairs(typeInfos) do
        if not modules:has(info.module) then
            modules:set(info.module, {})
        end
        local ____temp_0 = modules:get(info.module)
        ____temp_0[#____temp_0 + 1] = info.name
    end
    for ____, ____value in __TS__Iterator(modules) do
        local module = ____value[1]
        local names = ____value[2]
        local enums = __TS__ArrayFilter(
            names,
            function(____, n) return __TS__ArrayFind(
                typeInfos,
                function(____, t) return t.name == n and t.isEnum end
            ) end
        )
        local types = __TS__ArrayFilter(
            names,
            function(____, n) return __TS__ArrayFind(
                typeInfos,
                function(____, t) return t.name == n and not t.isEnum end
            ) end
        )
        if #enums > 0 then
            exportLines[#exportLines + 1] = ((("export { " .. table.concat(enums, ", ")) .. " } from './") .. module) .. "';"
        end
        if #types > 0 then
            exportLines[#exportLines + 1] = ((("export type { " .. table.concat(types, ", ")) .. " } from './") .. module) .. "';"
        end
    end
    for ____, ____value in __TS__Iterator(modules) do
        local module = ____value[1]
        local names = ____value[2]
        local enums = __TS__ArrayFilter(
            names,
            function(____, n) return __TS__ArrayFind(
                typeInfos,
                function(____, t) return t.name == n and t.isEnum end
            ) end
        )
        local types = __TS__ArrayFilter(
            names,
            function(____, n) return __TS__ArrayFind(
                typeInfos,
                function(____, t) return t.name == n and not t.isEnum end
            ) end
        )
        if #enums > 0 then
            importLines[#importLines + 1] = ((("import { " .. table.concat(enums, ", ")) .. " } from './") .. module) .. "';"
        end
        if #types > 0 then
            importLines[#importLines + 1] = ((("import type { " .. table.concat(types, ", ")) .. " } from './") .. module) .. "';"
        end
    end
    local protoLines = {
        "// 通用 create 辅助函数",
        "function createMessage<T>(defaults: Partial<T>, init?: Partial<T>): T {",
        "  return { ...defaults, ...init } as T;",
        "}",
        "",
        "// 创建 proto 对象（自动生成）",
        "export const proto = {"
    }
    for ____, ____value in __TS__Iterator(modules) do
        local module = ____value[1]
        local names = ____value[2]
        local moduleTypes = __TS__ArrayFilter(
            typeInfos,
            function(____, t) return t.module == module end
        )
        protoLines[#protoLines + 1] = ("  " .. module) .. ": {"
        for ____, name in ipairs(names) do
            do
                local __continue64
                repeat
                    local info = __TS__ArrayFind(
                        moduleTypes,
                        function(____, t) return t.name == name end
                    )
                    if not info then
                        __continue64 = true
                        break
                    end
                    if info.isEnum then
                        protoLines[#protoLines + 1] = ("    " .. name) .. ","
                    else
                        local defaults = generateDefaults(nil, info)
                        protoLines[#protoLines + 1] = ("    " .. name) .. ": {"
                        protoLines[#protoLines + 1] = ((("      create: (init?: Partial<" .. name) .. ">): ") .. name) .. " =>"
                        protoLines[#protoLines + 1] = ("        createMessage(" .. defaults) .. ", init),"
                        protoLines[#protoLines + 1] = "    },"
                    end
                    __continue64 = true
                until true
                if not __continue64 then
                    break
                end
            end
        end
        protoLines[#protoLines + 1] = "  },"
    end
    protoLines[#protoLines + 1] = "};"
    protoLines[#protoLines + 1] = ""
    protoLines[#protoLines + 1] = "export default proto;"
    return ((((("/**\n * Protocol Buffers TypeScript 定义\n * 由 ts-proto 自动生成\n * 源文件: protocols/proto/*.proto\n * 生成命令: npm run build:proto\n */\n\n" .. table.concat(exportLines, "\n")) .. "\n\n") .. table.concat(importLines, "\n")) .. "\n\n") .. table.concat(protoLines, "\n")) .. "\n"
end
function parseGeneratedTypes(self, protosDir)
    local types = {}
    local files = __TS__ArrayFilter(
        fs:readdirSync(protosDir),
        function(____, f) return __TS__StringEndsWith(f, ".ts") and f ~= "index.ts" end
    )
    for ____, file in ipairs(files) do
        local module = path:basename(file, ".ts")
        local content = fs:readFileSync(
            path:join(protosDir, file),
            "utf-8"
        )
        local enumRegex = nil
        local match
        while true do
            match = enumRegex:exec(content)
            if not (match ~= nil) then
                break
            end
            types[#types + 1] = {
                module = module,
                name = match[2],
                isEnum = true,
                isEnumType = false,
                fields = {}
            }
        end
        local interfaceRegex = nil
        while true do
            match = interfaceRegex:exec(content)
            if not (match ~= nil) then
                break
            end
            local name = match[2]
            local body = match[3]
            local fields = parseFields(nil, body)
            types[#types + 1] = {
                module = module,
                name = name,
                isEnum = false,
                isEnumType = __TS__ArraySome(
                    fields,
                    function(____, f) return __TS__StringIncludes(f.type, "ErrorCode") end
                ),
                fields = fields
            }
        end
    end
    return types
end
function parseFields(self, body)
    local fields = {}
    local lines = __TS__ArrayFilter(
        __TS__ArrayMap(
            __TS__StringSplit(body, "\n"),
            function(____, l) return __TS__StringTrim(l) end
        ),
        function(____, l) return l and not __TS__StringStartsWith(l, "*") and not __TS__StringStartsWith(l, "//") end
    )
    for ____, line in ipairs(lines) do
        local fieldMatch = line:match(nil)
        if fieldMatch then
            local name = fieldMatch[2]
            local ____type = __TS__StringTrim(__TS__StringReplace(fieldMatch[3], nil, ""))
            local isOptional = __TS__StringIncludes(line, "?")
            fields[#fields + 1] = {name = name, type = ____type, isOptional = isOptional}
        end
    end
    return fields
end
function generateDefaults(self, info)
    local defaults = {}
    for ____, field in ipairs(info.fields) do
        local defaultVal = getDefaultValue(nil, field.type, field.isOptional)
        defaults[#defaults + 1] = (field.name .. ": ") .. defaultVal
    end
    return ("{ " .. table.concat(defaults, ", ")) .. " }"
end
function getDefaultValue(self, ____type, isOptional)
    if isOptional then
        return "undefined"
    end
    local baseType = __TS__StringTrim(__TS__StringReplace(
        __TS__StringReplace(
            __TS__StringReplace(____type, nil, ""),
            nil,
            ""
        ),
        nil,
        ""
    ))
    if baseType == "string" then
        return "''"
    end
    if baseType == "number" or baseType == "long" then
        return "0"
    end
    if baseType == "boolean" then
        return "false"
    end
    if baseType == "Uint8Array" then
        return "new Uint8Array(0)"
    end
    if baseType == "ErrorCode" then
        return "ErrorCode.SUCCESS"
    end
    if __TS__StringIncludes(____type, "[]") then
        return "[]"
    end
    return "undefined"
end
local __filename = fileURLToPath(nil, nil.url)
local colors = {
    reset = "[0m",
    blue = "[34m",
    green = "[32m",
    yellow = "[33m",
    red = "[31m"
}
local function info(self, msg)
    console:log((((colors.blue .. "[INFO]") .. colors.reset) .. " ") .. msg)
end
local function success(self, msg)
    console:log((((colors.green .. "[SUCCESS]") .. colors.reset) .. " ") .. msg)
end
local function warn(self, msg)
    console:log((((colors.yellow .. "[WARN]") .. colors.reset) .. " ") .. msg)
end
local function ____error(self, msg)
    console:error((((colors.red .. "[ERROR]") .. colors.reset) .. " ") .. msg)
end
local function main(self)
    local scriptDir = path:dirname(__filename)
    local baseDir = path:resolve(scriptDir, "..")
    local configPath = path:join(baseDir, "proto.config.json")
    if not fs:existsSync(configPath) then
        ____error(nil, "Config file not found: " .. configPath)
        process:exit(1)
    end
    local config = JSON:parse(fs:readFileSync(configPath, "utf-8"))
    info(nil, "Loaded config: " .. configPath)
    console:log("")
    console:log("========================================")
    console:log("  Compiling Protocol Buffers")
    console:log("========================================")
    console:log("")
    local allProtoFiles = {}
    for ____, protoDir in ipairs(config.proto_dirs) do
        do
            local __continue8
            repeat
                local fullDir = path:resolve(baseDir, protoDir)
                if not fs:existsSync(fullDir) then
                    warn(nil, "Proto directory not found: " .. fullDir)
                    __continue8 = true
                    break
                end
                info(nil, "Scanning proto directory: " .. protoDir)
                local protoFiles = __TS__ArraySort(globSync(nil, "**/*.proto", {cwd = fullDir}))
                for ____, file in ipairs(protoFiles) do
                    allProtoFiles[#allProtoFiles + 1] = path:join(fullDir, file)
                end
                __continue8 = true
            until true
            if not __continue8 then
                break
            end
        end
    end
    if #allProtoFiles == 0 then
        ____error(nil, "No .proto files found")
        process:exit(1)
    end
    info(
        nil,
        ("Found " .. tostring(#allProtoFiles)) .. " proto files:"
    )
    for ____, file in ipairs(allProtoFiles) do
        console:log("  - " .. path:basename(file))
    end
    console:log("")
    local protocCmd = nil
    do
        local function ____catch()
            local localProtoc = path:join(baseDir, "bin", "protoc.exe")
            if fs:existsSync(localProtoc) then
                protocCmd = localProtoc
            end
        end
        local ____try = pcall(function()
            execSync(nil, "protoc --version", {stdio = "ignore"})
            protocCmd = "protoc"
        end)
        if not ____try then
            ____catch()
        end
    end
    if protocCmd then
        console:log("----------------------------------------")
        info(nil, "Generating Lua descriptor files...")
        console:log("----------------------------------------")
        for ____, luaDir in ipairs(config.output_lua) do
            local fullLuaDir = path:resolve(baseDir, luaDir)
            fs:mkdirSync(fullLuaDir, {recursive = true})
            info(nil, "Output directory: " .. luaDir)
            for ____, protoFile in ipairs(allProtoFiles) do
                local filename = path:basename(protoFile, ".proto")
                local protoPath = path:dirname(protoFile)
                local outFile = path:join(fullLuaDir, filename .. "_pb.desc")
                do
                    local function ____catch()
                        warn(nil, ((luaDir .. "/") .. filename) .. ".desc (failed)")
                    end
                    local ____try = pcall(function()
                        execSync(nil, ((((((("\"" .. protocCmd) .. "\" --proto_path=\"") .. protoPath) .. "\" --descriptor_set_out=\"") .. outFile) .. "\" --include_imports \"") .. protoFile) .. "\"", {stdio = "ignore"})
                        success(nil, ((luaDir .. "/") .. filename) .. ".desc")
                    end)
                    if not ____try then
                        ____catch()
                    end
                end
            end
        end
        console:log("")
    end
    local firstTsDir = path:resolve(baseDir, config.output_ts[1])
    if config.skip_ts_generation then
        info(nil, "Skipping TypeScript generation (manual mode)")
        console:log("")
    else
        console:log("----------------------------------------")
        info(nil, "Generating TypeScript files with ts-proto...")
        console:log("----------------------------------------")
        for ____, tsDir in ipairs(config.output_ts) do
            local fullTsDir = path:resolve(baseDir, tsDir)
            fs:mkdirSync(fullTsDir, {recursive = true})
            info(nil, "Output directory: " .. tsDir)
        end
        if not protocCmd then
            ____error(nil, "protoc not found")
            process:exit(1)
        end
        local projectRoot = path:resolve(baseDir, "..")
        local localPlugin = path:join(baseDir, "node_modules", ".bin", "protoc-gen-ts_proto.cmd")
        local localPluginAlt = path:join(baseDir, "node_modules", ".bin", "protoc-gen-ts_proto")
        local rootPlugin = path:join(projectRoot, "node_modules", ".bin", "protoc-gen-ts_proto.cmd")
        local rootPluginAlt = path:join(projectRoot, "node_modules", ".bin", "protoc-gen-ts_proto")
        local pluginPath
        if fs:existsSync(localPlugin) then
            pluginPath = localPlugin
        elseif fs:existsSync(localPluginAlt) then
            pluginPath = localPluginAlt
        elseif fs:existsSync(rootPlugin) then
            pluginPath = rootPlugin
        elseif fs:existsSync(rootPluginAlt) then
            pluginPath = rootPluginAlt
        else
            ____error(nil, "ts-proto plugin not found. Run: npm install ts-proto")
            process:exit(1)
        end
        info(nil, "Using protoc: " .. protocCmd)
        info(nil, "Using ts-proto plugin: " .. pluginPath)
        for ____, protoFile in ipairs(allProtoFiles) do
            local filename = path:basename(protoFile, ".proto")
            local protoPath = path:dirname(protoFile)
            do
                local function ____catch(err)
                    warn(nil, filename .. ".ts (failed)")
                    if __TS__InstanceOf(err, Error) then
                        console:error(err.message)
                    end
                end
                local ____try, ____hasReturned = pcall(function()
                    local args = {
                        "--plugin=protoc-gen-ts_proto=" .. pluginPath,
                        "--ts_proto_out=" .. firstTsDir,
                        "--ts_proto_opt=outputServices=false,onlyTypes=true,useExactTypes=false,stringEnums=false,useOptionals=messages,snakeToCamel=false",
                        "--proto_path=" .. protoPath,
                        protoFile
                    }
                    execSync(
                        nil,
                        (("\"" .. protocCmd) .. "\" ") .. table.concat(args, " "),
                        {stdio = "pipe"}
                    )
                    success(nil, filename .. ".ts")
                end)
                if not ____try then
                    ____catch(____hasReturned)
                end
            end
        end
    end
    if not config.skip_ts_generation then
        local indexTsPath = path:join(firstTsDir, "index.ts")
        local indexContent = generateIndexTs(nil, firstTsDir)
        fs:writeFileSync(indexTsPath, indexContent)
        success(nil, "index.ts (generated)")
    end
    console:log("")
    console:log("========================================")
    success(nil, "Protocol compilation complete!")
    console:log("========================================")
    console:log("")
end
main(nil)
return ____exports
