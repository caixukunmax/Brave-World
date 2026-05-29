shader_type canvas_item;

// 网格参数
uniform float grid_size = 142.0;
uniform float line_width = 1.5;
uniform float aa_softness = 1.0;
uniform vec4 line_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec2 map_size_world = vec2(7100.0, 7100.0);

void fragment() {
    // 网格属于地图底板，直接用底板本地坐标映射到世界格子。
    vec2 world_pos = UV * map_size_world;

    // 以格子坐标系计算到最近整数边界的距离。
    vec2 grid_coord = world_pos / grid_size;
    vec2 dist_to_line = abs(fract(grid_coord - 0.5) - 0.5);

    // 使用屏幕导数估算一个像素在网格坐标中对应的宽度，避免远视野下出现规则性漏线。
    vec2 pixel_span = max(fwidth(grid_coord), vec2(1e-6));
    vec2 half_width = pixel_span * max(line_width, 1.0) * 0.5;
    vec2 aa_span = pixel_span * max(aa_softness, 1e-6);
    vec2 axis_alpha = 1.0 - smoothstep(half_width, half_width + aa_span, dist_to_line);
    float alpha = max(axis_alpha.x, axis_alpha.y) * line_color.a;

    COLOR = vec4(line_color.rgb, alpha);
}
