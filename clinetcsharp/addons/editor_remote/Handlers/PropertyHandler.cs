using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ClinetCSharp.Editor.Remote
{
    /// <summary>
    /// 节点属性操作 API 处理器。
    /// 读取/设置节点属性，支持常见类型：int/float/bool/string/Vector2/Vector3/Color 等。
    /// </summary>
    public static class PropertyHandler
    {
        public static void RegisterAll(EditorHttpServer server)
        {
            server.RegisterHandler("GET", "/api/node/properties", GetProperties);
            server.RegisterHandler("PUT", "/api/node/property", SetProperty);
            server.RegisterHandler("GET", "/api/node/property_list", GetPropertyList);
        }

        // =============== GET /api/node/properties ===============
        /// <summary>
        /// 获取指定节点的一组属性值。
        /// Query: path=节点路径, names=属性名（逗号分隔，可选，不传返回全部）
        /// </summary>
        private static object GetProperties(JsonDocument doc)
        {
            var root = doc.RootElement;
            var nodePath = root.GetProperty("path").GetString();
            var node = SceneHandler.FindNodeByPath(nodePath);
            if (node == null) throw new Exception($"Node not found: {nodePath}");

            var names = root.TryGetProperty("names", out var namesElem)
                ? namesElem.GetString()?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                : null;

            var result = new Dictionary<string, object>();

            if (names != null && names.Length > 0)
            {
                foreach (var name in names)
                {
                    var trimmed = name.Trim();
                    if (node.HasMethod(trimmed)) continue; // 跳过方法
                    try
                    {
                        var val = node.Get(trimmed);
                        result[trimmed] = VariantToSerializable(val);
                    }
                    catch
                    {
                        result[trimmed] = null;
                    }
                }
            }
            else
            {
                // 返回所有属性
                foreach (var prop in node.GetPropertyList())
                {
                    var name = prop["name"].AsString();
                    var usage = (PropertyUsageFlags)prop["usage"].AsInt32();
                    if (!usage.HasFlag(PropertyUsageFlags.Editor)) continue;
                    if (usage.HasFlag(PropertyUsageFlags.Internal)) continue;
                    try
                    {
                        var val = node.Get(name);
                        result[name.ToString()] = VariantToSerializable(val);
                    }
                    catch
                    {
                        result[name.ToString()] = null;
                    }
                }
            }

            return new { path = nodePath, properties = result };
        }

        // =============== PUT /api/node/property ===============
        /// <summary>
        /// 设置节点属性。
        /// Body: { path, name, value }
        /// value 会根据属性的类型自动转换。
        /// </summary>
        private static object SetProperty(JsonDocument doc)
        {
            var root = doc.RootElement;
            var nodePath = root.GetProperty("path").GetString();
            var propName = root.GetProperty("name").GetString();
            var valueElem = root.GetProperty("value");

            var node = SceneHandler.FindNodeByPath(nodePath);
            if (node == null) throw new Exception($"Node not found: {nodePath}");

            // 先获取属性的类型信息
            Variant targetValue;
            var oldValue = node.Get(propName);
            var oldType = oldValue.VariantType;

            try
            {
                targetValue = JsonElementToVariant(valueElem, oldType);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert value for property '{propName}' (expected {oldType}): {ex.Message}");
            }

            node.Set(propName, targetValue);

            return new
            {
                path = nodePath,
                property = propName,
                old_value = VariantToSerializable(oldValue),
                new_value = VariantToSerializable(node.Get(propName))
            };
        }

        // =============== GET /api/node/property_list ===============
        /// <summary>
        /// 获取节点的全部属性定义列表（类型、默认值、使用范围等）。
        /// </summary>
        private static object GetPropertyList(JsonDocument doc)
        {
            var nodePath = doc.RootElement.GetProperty("path").GetString();
            var node = SceneHandler.FindNodeByPath(nodePath);
            if (node == null) throw new Exception($"Node not found: {nodePath}");

            var list = new List<Dictionary<string, object>>();
            foreach (var prop in node.GetPropertyList())
            {
                var usage = (PropertyUsageFlags)prop["usage"].AsInt32();
                if (!usage.HasFlag(PropertyUsageFlags.Editor)) continue;
                if (usage.HasFlag(PropertyUsageFlags.Internal)) continue;

                var dict = new Dictionary<string, object>
                {
                    ["name"] = prop["name"].AsString(),
                    ["type"] = prop["type"].AsInt32().ToString(),
                    ["type_name"] = ((Variant.Type)prop["type"].AsInt32()).ToString(),
                    ["hint"] = ((PropertyHint)prop["hint"].AsInt32()).ToString(),
                    ["hint_string"] = prop["hint_string"].AsString(),
                    ["class_name"] = prop["class_name"].AsString(),
                };
                list.Add(dict);
            }

            return new { path = nodePath, properties = list };
        }

        // =============== 类型转换 ===============

        private static object VariantToSerializable(Variant v)
        {
            switch (v.VariantType)
            {
                case Variant.Type.Nil: return null;
                case Variant.Type.Bool: return v.AsBool();
                case Variant.Type.Int: return v.AsInt64();
                case Variant.Type.Float: return v.AsDouble();
                case Variant.Type.String: return v.AsString();
                case Variant.Type.Vector2:
                    var v2 = v.AsVector2();
                    return new { x = v2.X, y = v2.Y, _type = "Vector2" };
                case Variant.Type.Vector2I:
                    var v2i = v.AsVector2I();
                    return new { x = v2i.X, y = v2i.Y, _type = "Vector2I" };
                case Variant.Type.Vector3:
                    var v3 = v.AsVector3();
                    return new { x = v3.X, y = v3.Y, z = v3.Z, _type = "Vector3" };
                case Variant.Type.Vector3I:
                    var v3i = v.AsVector3I();
                    return new { x = v3i.X, y = v3i.Y, z = v3i.Z, _type = "Vector3I" };
                case Variant.Type.Color:
                    var c = v.AsColor();
                    return new { r = c.R, g = c.G, b = c.B, a = c.A, _type = "Color" };
                case Variant.Type.Rect2:
                    var r = v.AsRect2();
                    return new { x = r.Position.X, y = r.Position.Y, w = r.Size.X, h = r.Size.Y, _type = "Rect2" };
                default:
                    return v.ToString();
            }
        }

        private static Variant JsonElementToVariant(JsonElement elem, Variant.Type targetType)
        {
            switch (targetType)
            {
                case Variant.Type.Bool:
                    return Variant.From(elem.GetBoolean());
                case Variant.Type.Int:
                    return Variant.From(elem.GetInt64());
                case Variant.Type.Float:
                    return Variant.From(elem.GetDouble());
                case Variant.Type.String:
                    return Variant.From(elem.GetString() ?? "");
                case Variant.Type.Vector2:
                {
                    float x = 0, y = 0;
                    if (elem.TryGetProperty("x", out var px)) x = px.GetSingle();
                    if (elem.TryGetProperty("y", out var py)) y = py.GetSingle();
                    return Variant.From(new Vector2(x, y));
                }
                case Variant.Type.Vector2I:
                {
                    int x = 0, y = 0;
                    if (elem.TryGetProperty("x", out var px)) x = px.GetInt32();
                    if (elem.TryGetProperty("y", out var py)) y = py.GetInt32();
                    return Variant.From(new Vector2I(x, y));
                }
                case Variant.Type.Vector3:
                {
                    float x = 0, y = 0, z = 0;
                    if (elem.TryGetProperty("x", out var px)) x = px.GetSingle();
                    if (elem.TryGetProperty("y", out var py)) y = py.GetSingle();
                    if (elem.TryGetProperty("z", out var pz)) z = pz.GetSingle();
                    return Variant.From(new Vector3(x, y, z));
                }
                case Variant.Type.Color:
                {
                    float r = 0, g = 0, b = 0, a = 1;
                    if (elem.TryGetProperty("r", out var pr)) r = pr.GetSingle();
                    if (elem.TryGetProperty("g", out var pg)) g = pg.GetSingle();
                    if (elem.TryGetProperty("b", out var pb)) b = pb.GetSingle();
                    if (elem.TryGetProperty("a", out var pa)) a = pa.GetSingle();
                    return Variant.From(new Color(r, g, b, a));
                }
                case Variant.Type.NodePath:
                    return Variant.From(new NodePath(elem.GetString() ?? ""));
                default:
                    // 默认尝试用字符串
                    return Variant.From(elem.ToString());
            }
        }
    }
}
