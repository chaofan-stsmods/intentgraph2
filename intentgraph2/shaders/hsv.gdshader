shader_type canvas_item;

uniform float h: hint_range(0,1) = 1;
uniform float s: hint_range(0,5) = 1;
uniform float v = 1;

varying vec4 modulate_color;

void vertex() {
    modulate_color = COLOR;
}

void fragment() {
	// stub
    vec4 col = texture(TEXTURE, UV);
    COLOR = col;
    COLOR *= modulate_color;
}