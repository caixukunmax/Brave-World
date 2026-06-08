shader_type canvas_item;

// 网格参数
uniform float grid_size = 142.0;
uniform float line_width = 1.5;
uniform float aa_softness = 1.0;
uniform vec4 line_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec2 map_size_world = vec2(7100.0, 7100.0);

// 地形遮罩 + 颜色：RGBA8 纹理，每个像素对应一个格子
// R 通道：mask 值（0=地形墙/不绘制网格线并显示灰色填充，非0=普通格子）
// GBA 通道：地形颜色（RGB），当 mask==0 时作为填充色
uniform sampler2D terrain_mask : hint_default_white;
uniform vec2 terrain_mask_size = vec2(1.0, 1.0);

// 地图外部 / 地形墙填充颜色
uniform vec4 outside_map_color : source_color = vec4(0.15, 0.15, 0.15, 1.0);

void fragment() {
    vec2 world_pos = UV * map_size_world;

    // 计算当前像素所在的格子坐标（限制在有效范围内）
    vec2 grid_coord = world_pos / grid_size;
    ivec2 cell_coord = ivec2(clamp(floor(grid_coord), vec2(0.0), terrain_mask_size - vec2(1.0)));

    // 采样遮罩纹理：R=遮罩(0=墙), GBA=地形颜色(RGB)
    vec2 mask_uv = (vec2(cell_coord) + 0.5) / terrain_mask_size;
    vec4 mask_sample = texture(terrain_mask, mask_uv);
    float mask_value = mask_sample.r;
    vec3 terrain_color = mask_sample.gba;

    // 以格子坐标系计算到最近整数边界的距离
    vec2 dist_to_line = abs(fract(grid_coord - 0.5) - 0.5);

    // 使用屏幕导数估算一个像素在网格坐标中对应的宽度
    vec2 pixel_span = max(fwidth(grid_coord), vec2(1e-6));
    vec2 half_width = pixel_span * max(line_width, 1.0) * 0.5;
    vec2 aa_span = pixel_span * max(aa_softness, 1e-6);
    vec2 axis_alpha = 1.0 - smoothstep(half_width, half_width + aa_span, dist_to_line);
    float grid_alpha = max(axis_alpha.x, axis_alpha.y) * line_color.a;

    // 地图外部检测
    bool outside_map = world_pos.x < 0.0 || world_pos.x > map_size_world.x ||
                       world_pos.y < 0.0 || world_pos.y > map_size_world.y;

    // 地形墙或地图外：显示灰色填充，无网格线
    if (mask_value < 0.5 || outside_map) {
        COLOR = outside_map_color;
        return;
    }

    // 有地形颜色的格子：显示地形颜色背景 + 网格线叠加
    if (terrain_color.r > 0.001 || terrain_color.g > 0.001 || terrain_color.b > 0.001) {
        vec4 bg = vec4(terrain_color, 1.0);
        COLOR = mix(bg, vec4(line_color.rgb, 1.0), grid_alpha);
    } else {
        // 无特殊地形颜色：透明背景 + 网格线
        COLOR = vec4(line_color.rgb, grid_alpha);
    }
}
