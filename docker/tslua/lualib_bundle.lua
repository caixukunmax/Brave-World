local function __TS__StringIncludes(self, searchString, position)
    if not position then
        position = 1
    else
        position = position + 1
    end
    local index = string.find(self, searchString, position, true)
    return index ~= nil
end

local __TS__Match = string.match

local function __TS__SourceMapTraceBack(fileName, sourceMap)
    _G.__TS__sourcemap = _G.__TS__sourcemap or ({})
    _G.__TS__sourcemap[fileName] = sourceMap
    if _G.__TS__originalTraceback == nil then
        local originalTraceback = debug.traceback
        _G.__TS__originalTraceback = originalTraceback
        debug.traceback = function(thread, message, level)
            local trace
            if thread == nil and message == nil and level == nil then
                trace = originalTraceback()
            elseif __TS__StringIncludes(_VERSION, "Lua 5.0") then
                trace = originalTraceback((("[Level " .. tostring(level)) .. "] ") .. tostring(message))
            else
                trace = originalTraceback(thread, message, level)
            end
            if type(trace) ~= "string" then
                return trace
            end
            local function replacer(____, file, srcFile, line)
                local fileSourceMap = _G.__TS__sourcemap[file]
                if fileSourceMap ~= nil and fileSourceMap[line] ~= nil then
                    local data = fileSourceMap[line]
                    if type(data) == "number" then
                        return (srcFile .. ":") .. tostring(data)
                    end
                    return (data.file .. ":") .. tostring(data.line)
                end
                return (file .. ":") .. line
            end
            local result = string.gsub(
                trace,
                "([^%s<]+)%.lua:(%d+)",
                function(file, line) return replacer(nil, file .. ".lua", file .. ".ts", line) end
            )
            local function stringReplacer(____, file, line)
                local fileSourceMap = _G.__TS__sourcemap[file]
                if fileSourceMap ~= nil and fileSourceMap[line] ~= nil then
                    local chunkName = (__TS__Match(file, "%[string \"([^\"]+)\"%]"))
                    local sourceName = string.gsub(chunkName, ".lua$", ".ts")
                    local data = fileSourceMap[line]
                    if type(data) == "number" then
                        return (sourceName .. ":") .. tostring(data)
                    end
                    return (data.file .. ":") .. tostring(data.line)
                end
                return (file .. ":") .. line
            end
            result = string.gsub(
                result,
                "(%[string \"[^\"]+\"%]):(%d+)",
                function(file, line) return stringReplacer(nil, file, line) end
            )
            return result
        end
    end
end

local function __TS__Class(self)
    local c = {prototype = {}}
    c.prototype.__index = c.prototype
    c.prototype.constructor = c
    return c
end

local __TS__Symbol, Symbol
do
    local symbolMetatable = {__tostring = function(self)
        return ("Symbol(" .. (self.description or "")) .. ")"
    end}
    function __TS__Symbol(description)
        return setmetatable({description = description}, symbolMetatable)
    end
    Symbol = {
        asyncDispose = __TS__Symbol("Symbol.asyncDispose"),
        dispose = __TS__Symbol("Symbol.dispose"),
        iterator = __TS__Symbol("Symbol.iterator"),
        hasInstance = __TS__Symbol("Symbol.hasInstance"),
        species = __TS__Symbol("Symbol.species"),
        toStringTag = __TS__Symbol("Symbol.toStringTag")
    }
end

local __TS__Iterator
do
    local function iteratorGeneratorStep(self)
        local co = self.____coroutine
        local status, value = coroutine.resume(co)
        if not status then
            error(value, 0)
        end
        if coroutine.status(co) == "dead" then
            return
        end
        return true, value
    end
    local function iteratorIteratorStep(self)
        local result = self:next()
        if result.done then
            return
        end
        return true, result.value
    end
    local function iteratorStringStep(self, index)
        index = index + 1
        if index > #self then
            return
        end
        return index, string.sub(self, index, index)
    end
    function __TS__Iterator(iterable)
        if type(iterable) == "string" then
            return iteratorStringStep, iterable, 0
        elseif iterable.____coroutine ~= nil then
            return iteratorGeneratorStep, iterable
        elseif iterable[Symbol.iterator] then
            local iterator = iterable[Symbol.iterator](iterable)
            return iteratorIteratorStep, iterator
        else
            return ipairs(iterable)
        end
    end
end

local Map
do
    Map = __TS__Class()
    Map.name = "Map"
    function Map.prototype.____constructor(self, entries)
        self[Symbol.toStringTag] = "Map"
        self.items = {}
        self.size = 0
        self.nextKey = {}
        self.previousKey = {}
        if entries == nil then
            return
        end
        local iterable = entries
        if iterable[Symbol.iterator] then
            local iterator = iterable[Symbol.iterator](iterable)
            while true do
                local result = iterator:next()
                if result.done then
                    break
                end
                local value = result.value
                self:set(value[1], value[2])
            end
        else
            local array = entries
            for ____, kvp in ipairs(array) do
                self:set(kvp[1], kvp[2])
            end
        end
    end
    function Map.prototype.clear(self)
        self.items = {}
        self.nextKey = {}
        self.previousKey = {}
        self.firstKey = nil
        self.lastKey = nil
        self.size = 0
    end
    function Map.prototype.delete(self, key)
        local contains = self:has(key)
        if contains then
            self.size = self.size - 1
            local next = self.nextKey[key]
            local previous = self.previousKey[key]
            if next ~= nil and previous ~= nil then
                self.nextKey[previous] = next
                self.previousKey[next] = previous
            elseif next ~= nil then
                self.firstKey = next
                self.previousKey[next] = nil
            elseif previous ~= nil then
                self.lastKey = previous
                self.nextKey[previous] = nil
            else
                self.firstKey = nil
                self.lastKey = nil
            end
            self.nextKey[key] = nil
            self.previousKey[key] = nil
        end
        self.items[key] = nil
        return contains
    end
    function Map.prototype.forEach(self, callback)
        for ____, key in __TS__Iterator(self:keys()) do
            callback(nil, self.items[key], key, self)
        end
    end
    function Map.prototype.get(self, key)
        return self.items[key]
    end
    function Map.prototype.has(self, key)
        return self.nextKey[key] ~= nil or self.lastKey == key
    end
    function Map.prototype.set(self, key, value)
        local isNewValue = not self:has(key)
        if isNewValue then
            self.size = self.size + 1
        end
        self.items[key] = value
        if self.firstKey == nil then
            self.firstKey = key
            self.lastKey = key
        elseif isNewValue then
            self.nextKey[self.lastKey] = key
            self.previousKey[key] = self.lastKey
            self.lastKey = key
        end
        return self
    end
    Map.prototype[Symbol.iterator] = function(self)
        return self:entries()
    end
    function Map.prototype.entries(self)
        local items = self.items
        local nextKey = self.nextKey
        local key = self.firstKey
        return {
            [Symbol.iterator] = function(self)
                return self
            end,
            next = function(self)
                local result = {done = not key, value = {key, items[key]}}
                key = nextKey[key]
                return result
            end
        }
    end
    function Map.prototype.keys(self)
        local nextKey = self.nextKey
        local key = self.firstKey
        return {
            [Symbol.iterator] = function(self)
                return self
            end,
            next = function(self)
                local result = {done = not key, value = key}
                key = nextKey[key]
                return result
            end
        }
    end
    function Map.prototype.values(self)
        local items = self.items
        local nextKey = self.nextKey
        local key = self.firstKey
        return {
            [Symbol.iterator] = function(self)
                return self
            end,
            next = function(self)
                local result = {done = not key, value = items[key]}
                key = nextKey[key]
                return result
            end
        }
    end
    Map[Symbol.species] = Map
end

local function __TS__New(target, ...)
    local instance = setmetatable({}, target.prototype)
    instance:____constructor(...)
    return instance
end

local function __TS__Number(value)
    local valueType = type(value)
    if valueType == "number" then
        return value
    elseif valueType == "string" then
        local numberValue = tonumber(value)
        if numberValue then
            return numberValue
        end
        if value == "Infinity" then
            return math.huge
        end
        if value == "-Infinity" then
            return -math.huge
        end
        local stringWithoutSpaces = string.gsub(value, "%s", "")
        if stringWithoutSpaces == "" then
            return 0
        end
        return 0 / 0
    elseif valueType == "boolean" then
        return value and 1 or 0
    else
        return 0 / 0
    end
end

local function __TS__ObjectAssign(target, ...)
    local sources = {...}
    for i = 1, #sources do
        local source = sources[i]
        for key in pairs(source) do
            target[key] = source[key]
        end
    end
    return target
end

return {
  __TS__SourceMapTraceBack = __TS__SourceMapTraceBack,
  Map = Map,
  __TS__New = __TS__New,
  __TS__Class = __TS__Class,
  __TS__Number = __TS__Number,
  __TS__ObjectAssign = __TS__ObjectAssign
}
