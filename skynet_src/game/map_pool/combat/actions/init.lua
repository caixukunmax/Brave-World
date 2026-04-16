-- Action 注册中心
local ActionRegistry = {}
ActionRegistry.handlers = {}

function ActionRegistry:register(name, actionModule)
    self.handlers[name] = actionModule
end

function ActionRegistry:get(name)
    return self.handlers[name]
end

return ActionRegistry
