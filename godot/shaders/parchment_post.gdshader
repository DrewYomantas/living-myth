// Living Myth — North Star diorama post: warm parchment grade, soft vignette, fine grain.
// A subtle, serious finishing pass (NOT a gimmick filter). Pulls a Cycles-lit low-poly
// diorama toward the illustrated, material-rich North Star look.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float vignette : hint_range(0.0, 1.0) = 0.42;
uniform float grain : hint_range(0.0, 0.2) = 0.035;
uniform float warmth : hint_range(0.0, 1.0) = 0.5;
uniform vec3 paper_tint = vec3(1.06, 1.01, 0.9);
uniform vec3 shadow_tint = vec3(0.18, 0.13, 0.09);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec3 c = texture(screen_tex, SCREEN_UV).rgb;

	// warm parchment grade — push midtones toward aged paper, tint the shadows sepia
	vec3 warm = c * paper_tint;
	float luma = dot(c, vec3(0.299, 0.587, 0.114));
	warm = mix(warm, warm + shadow_tint * (1.0 - luma) * 0.35, 0.6);
	c = mix(c, warm, warmth);

	// soft round vignette
	float d = distance(SCREEN_UV, vec2(0.5));
	c *= 1.0 - vignette * smoothstep(0.32, 0.92, d);

	// fine film grain, luma-weighted so it lives in the midtones
	float n = hash(SCREEN_UV * vec2(1920.0, 1080.0));
	c += (n - 0.5) * grain * (0.4 + 0.6 * (1.0 - abs(luma - 0.5) * 2.0));

	COLOR = vec4(c, 1.0);
}
