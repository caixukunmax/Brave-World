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

// 水面遮罩：R=1 表示水域，R=0 表示非水域
uniform sampler2D water_mask : hint_default_black;
uniform vec2 water_mask_size = vec2(1.0, 1.0);

// 水面动画参数：可见但柔和的波纹
uniform float water_speed = 0.05;
uniform float water_wave_strength = 0.045;
uniform float water_sparkle_speed = 0.12;
uniform float water_sparkle_intensity = 0.035;

// 地图外部 / 地形墙填充颜色
uniform vec4 outside_map_color : source_color = vec4(0.15, 0.15, 0.15, 1.0);

// 平滑 hash noise
float water_hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float water_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(water_hash(i), water_hash(i + vec2(1.0, 0.0)), u.x),
               mix(water_hash(i + vec2(0.0, 1.0)), water_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float water_fbm(vec2 p) {
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 5; i++) {
        value += amplitude * water_noise(p);
        p *= 2.2;
        amplitude *= 0.5;
    }
    return value;
}

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

    // 采样水面遮罩
    vec2 water_uv = (vec2(cell_coord) + 0.5) / water_mask_size;
    float is_water = texture(water_mask, water_uv).r;

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
    // 注意：CanvasItem shader 中不要使用 return，否则行为未定义
    vec4 final_color;
    if (mask_value < 0.5 || outside_map) {
        final_color = outside_map_color;
    } else if (terrain_color.r > 0.001 || terrain_color.g > 0.001 || terrain_color.b > 0.001) {
        vec4 bg = vec4(terrain_color, 1.0);

        // 水面动画：柔和可见的波纹 + 偶尔微光
        if (is_water > 0.5) {
            float t = TIME * water_speed;

            // 低频噪声驱动波纹形状，避免死板正弦条纹
            vec2 noise_uv = world_pos * 0.018 + vec2(t, t * 0.35);
            float n = water_fbm(noise_uv);

            // 用噪声调制正弦波相位，形成自然水纹
            float phase = n * 3.14159;
            float w1 = sin(world_pos.x * 0.035 + world_pos.y * 0.015 + t + phase) * water_wave_strength;
            float w2 = sin(world_pos.x * 0.022 - world_pos.y * 0.028 + t * 0.8 - phase * 0.7) * (water_wave_strength * 0.7);
            float wave = (w1 + w2) * 0.5;

            // 偶尔的柔和微光，不是细碎斑点
            float sparkle_phase = water_fbm(world_pos * 0.035 + vec2(TIME * water_sparkle_speed, -TIME * water_sparkle_speed * 0.5));
            float sparkle = pow(max(0.0, sparkle_phase - 0.55), 3.0) * water_sparkle_intensity;

            // 蓝色通道变化更明显，整体柔和
            bg.rgb += vec3(wave * 0.45 + sparkle, wave * 0.55 + sparkle, wave * 0.95 + sparkle * 1.2);
        }

        final_color = mix(bg, vec4(line_color.rgb, 1.0), grid_alpha);
    } else {
        // 有效格子但地形颜色为纯黑（如普通地形 color_a=0）：用极暗灰填充，避免看起来像空数据
        vec4 default_fill = vec4(0.06, 0.06, 0.06, 1.0);
        final_color = mix(default_fill, vec4(line_color.rgb, 1.0), grid_alpha);
    }

    COLOR = final_color;
}
