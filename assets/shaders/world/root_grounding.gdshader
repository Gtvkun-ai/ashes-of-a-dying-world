shader_type canvas_item;
render_mode unshaded, blend_mul;

// V12.2 - adaptive root-ground stain.
// Multiplies whatever terrain is already underneath (grass, dirt, path), so the
// grounding reads as compressed/darker ground instead of a separate black shadow decal.
uniform float strength : hint_range(0.0, 1.0) = 0.78;
uniform vec3 stain_multiplier : source_color = vec3(0.72, 0.78, 0.68);

void fragment() {
    float mask = texture(TEXTURE, UV).a;
    float amount = clamp(mask * strength, 0.0, 1.0);
    vec3 multiplier = mix(vec3(1.0), stain_multiplier, amount);
    COLOR = vec4(multiplier, 1.0);
}
