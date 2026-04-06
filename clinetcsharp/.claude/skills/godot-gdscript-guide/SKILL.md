---
name: godot-gdscript-guide
description: |
  Godot 4 GDScript 开发指南 — 包含语法参考、最佳实践和常见陷阱。
  适用于 Godot 4.x 项目开发，提供代码风格指导和调试技巧。
---

# Godot 4 GDScript 开发指南

本指南提取自 godogen 项目，针对 Godot 4.x 优化。

## 类型系统

### 基础类型

```gdscript
# 基本类型
null, bool, int, float, String, StringName, NodePath

# 向量
Vector2, Vector2i, Vector3, Vector3i, Vector4, Vector4i

# 变换
Transform2D, Transform3D, Basis, Quaternion

# 几何
Rect2, Rect2i, AABB, Plane

# 容器
Array, Array[Type], Dictionary, Dictionary[K, V]

# 数组类型
PackedByteArray, PackedInt32Array, PackedFloat32Array,
PackedStringArray, PackedVector2Array, PackedVector3Array
```

### 值类型 vs 引用类型

```gdscript
# 值类型（复制传递）
bool, int, float, Vector2/3, AABB, Transform2D/3D, Color, Rect2

# 引用类型（共享引用）
Object, Array, Dictionary, packed arrays
# 复制引用类型用 duplicate()
var arr_copy = original_array.duplicate()
```

### 类型推断陷阱

```gdscript
# 错误：无法从 Variant 推断类型
var bad := abs(speed)
var bad2 := clamp(val, 0.0, 1.0)
var bad3 := positions[i]  # 数组访问返回 Variant

# 正确：显式类型声明
var good: float = abs(speed)
var pos: Vector3 = positions[i]

# 或使用无类型（Variant）
var also_ok = abs(speed)
```

## 变量初始化顺序

```
1. 类型默认值（或 null）
2. 声明时初始化器（按声明顺序）
3. _init()
4. 导出值（场景/资源中设置）
5. @onready 值
6. _ready()
```

注意：`@onready` 与 `@export` 不要一起用，会触发警告。

## 属性（getter/setter）

```gdscript
var score: int:
    get:
        return score
    set(value):
        score = clamp(value, 0, 100)
        score_changed.emit(score)

# 初始值不会触发 setter
var x: int = 5  # setter 不会被调用
```

## 节点引用

```gdscript
$NodeName                    # 获取子节点
$Path/To/Node                # 嵌套路径
%UniqueNode                  # 场景唯一节点（编辑器中设置）
get_node_or_null("Path")     # 安全获取，不存在返回 null
has_node("Path")             # 检查存在性
```

## 信号

```gdscript
# 定义
signal health_changed(new_value: int)

# 发射
health_changed.emit(42)

# 连接
other_node.my_signal.connect(_on_signal)
other_node.my_signal.connect(func(): print("lambda"))

# 带参数绑定
other_node.my_signal.connect(_on_signal.bind("extra_data"))

# 等待
await my_signal
await get_tree().create_timer(1.0).timeout
```

## 内存管理

```gdscript
# RefCounted（自动释放）
# Array, Dictionary, Resource

# Object/Node（手动管理）
node.free()           # 立即删除
node.queue_free()     # 帧末安全删除（推荐）

# 检查有效性
if is_instance_valid(obj):  # 检查是否已被释放

# 弱引用
var weak = weakref(obj)  # 不阻止释放
```

## 常见陷阱

### 1. Lambda 捕获行为

```gdscript
var x = 42
var arr = []
var fn = func():
    print(x)       # 永远是 42，即使外部 x 改变
    arr.append(1)  # 共享引用，内容变化同步
    x = 99         # 警告：只影响 lambda 的副本
```

### 2. 类型推断与 instantiate

```gdscript
# 错误
var scene := load("res://enemy.tscn")
var model := scene.instantiate()

# 正确
var scene: PackedScene = load("res://enemy.tscn")
var model = scene.instantiate()  # 不用 :=
```

### 3. @onready 时序

```gdscript
# 不可靠
@onready var x = $Node if has_node("Node") else null

# 安全
var x: Node = null
func _ready():
    x = get_node_or_null("Node")
```

### 4. 碰撞状态变更

```gdscript
# 错误：在回调中修改碰撞状态
func _on_body_entered(body):
    $CollisionShape.disabled = true  # 报错！

# 正确：延迟修改
func _on_body_entered(body):
    set_deferred("disabled", true)
    # 或
    $CollisionShape.set_deferred("disabled", true)
```

### 5. 碰撞层位掩码

```gdscript
# UI 层 1 = 位掩码 1
# UI 层 2 = 位掩码 2
# UI 层 3 = 位掩码 4（2 的幂）

# 设置第 3 层
collision_layer = 4  # 不是 3！
```

### 6. Camera2D 激活

```gdscript
# 没有 current 属性
$Camera2D.current = true

# 使用方法
$Camera2D.make_current()  # 必须在场景树中后调用
```

### 7. CharacterBody 移动模式

```gdscript
# 2D 俯视游戏必须设置
motion_mode = MOTION_MODE_FLOATING  # 禁用重力和地板检测

# 3D 非平台移动也需要（车辆、滑雪板）
```

## 实用模式

### 状态机

```gdscript
class State extends Node:
    signal finished(next_state: StringName)
    func enter() -> void: pass
    func exit() -> void: pass
    func update(_delta: float) -> void: pass

# 使用栈管理临时状态（攻击、硬直）
var states_stack: Array[State]
```

### 平滑移动

```gdscript
# 不对称加减速（更有质感）
if abs(input_dir) > 0.2:
    velocity.x = move_toward(velocity.x, input_dir * MAX_SPEED, ACCEL * delta)
else:
    velocity.x = move_toward(velocity.x, 0, DECEL * delta)  # 不同的减速度

# 速度限制
velocity.x = clamp(velocity.x, -MAX_SPEED, MAX_SPEED)
```

### 相机平滑跟随

```gdscript
# 避免第一帧从原点飞入
var _initialized := false

func _physics_process(delta: float) -> void:
    if not _initialized:
        global_position = target.global_position  # 第一帧直接定位
        _initialized = true
        return

    # 后续帧使用平滑
    global_position = global_position.lerp(target.global_position, speed * delta)
```

### 自定义绘制

```gdscript
func _draw() -> void:
    draw_line(from, to, color, width)
    draw_circle(pos, radius, color, filled, width)
    draw_rect(Rect2(pos, size), color, filled, width)
    draw_texture(texture, pos, modulate)

func _process(_delta: float) -> void:
    queue_redraw()  # 触发重绘
```

### Tween 动画

```gdscript
var tween = create_tween()
tween.set_trans(Tween.TRANS_QUAD)
tween.set_ease(Tween.EASE_OUT)

# 并行动画
tween.parallel().tween_property(node, "position", target_pos, 0.5)
tween.parallel().tween_property(node, "modulate", Color.RED, 0.5)

# 回调
tween.tween_callback(method.bind(args))
tween.finished.connect(_on_complete)
```

### 网格对齐

```gdscript
# 移动时辅助对齐，防止卡角
if moving_horizontally:
    position.y = round(position.y / tile_size) * tile_size + tile_size / 2
if moving_vertically:
    position.x = round(position.x / tile_size) * tile_size + tile_size / 2
```

### 文件 I/O

```gdscript
# 写入
var f = FileAccess.open(path, FileAccess.WRITE)
f.store_string(data)
f.close()

# 读取
var text = FileAccess.get_file_as_string(path)

# 用户数据路径
var user_path = "user://config.cfg"  # 自动映射到用户目录
```

## 调试技巧

```gdscript
# 断点
breakpoint

# 断言（发布版自动移除）
assert(x > 0, "x must be positive")

# 标记（编辑器中高亮）
# TODO: 待办
# FIXME: 需要修复
# BUG: 已知问题
# NOTE: 重要说明
# HACK: 临时方案
```

## 代码风格

- 使用 `snake_case` 命名函数和变量
- 使用 `PascalCase` 命名类
- 使用 `ALL_CAPS` 命名常量
- 类型声明优先，必要时用显式类型替代 `:=`
- 信号用过去分词：`health_changed`, `player_died`
- 私有变量用 `_` 前缀：`_private_var`

## 参考资源

- [Godot 4 官方文档](https://docs.godotengine.org/en/stable/)
- [GDScript 风格指南](https://docs.godotengine.org/en/stable/tutorials/scripting/gdscript/gdscript_styleguide.html)
- 完整 API 参考见 Godot 编辑器帮助