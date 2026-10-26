using Godot;

/// <summary>
/// The deep-space backdrop of the title screen, drawn entirely by shaders: slow nebula clouds, three layers of
/// twinkling stars, an occasional shooting star and a lit planet with an atmosphere. No image files needed.
/// </summary>
public partial class SpaceBackground : Control
{
	private const string SpaceShader = @"
shader_type canvas_item;

uniform float aspect = 1.7778;
uniform vec4 deep_a : source_color = vec4(0.012, 0.016, 0.040, 1.0);
uniform vec4 deep_b : source_color = vec4(0.045, 0.020, 0.095, 1.0);
uniform vec4 violet : source_color = vec4(0.50, 0.16, 0.72, 1.0);
uniform vec4 teal : source_color = vec4(0.07, 0.46, 0.62, 1.0);
uniform vec4 rose : source_color = vec4(0.85, 0.32, 0.38, 1.0);

// Integer hash: float hashes with fract() give different results for the same input when the GPU compiler
// reorders the arithmetic, which showed up as hard edges in the clouds.
float hash21(vec2 p) {
	uvec2 q = floatBitsToUint(p);
	uint h = q.x * 1597334673u + q.y * 3812015801u;
	h ^= h >> 16u;
	h *= 2146121005u;
	h ^= h >> 15u;
	h *= 2221713035u;
	h ^= h >> 16u;
	return float(h) * 2.3283064e-10;
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 5; i++) {
		v += a * noise(p);
		p = p * 2.03 + vec2(17.0, 9.0);
		a *= 0.5;
	}
	return v;
}

vec3 star_layer(vec2 uv, float density, float size, float t, float seed) {
	vec2 g = uv * density;
	vec2 id = floor(g);
	vec2 f = fract(g) - 0.5;
	float h = hash21(id + seed);
	vec2 offs = (vec2(hash21(id + seed + 3.1), hash21(id + seed + 7.7)) - 0.5) * 0.6;
	float d = length(f - offs);
	float twinkle = 0.55 + 0.45 * sin(t * (1.0 + h * 3.0) + h * 40.0);
	float s = smoothstep(size, 0.0, d) * step(0.80, h) * twinkle;
	vec3 tint = mix(vec3(0.75, 0.88, 1.0), vec3(1.0, 0.86, 0.66), hash21(id + seed + 11.0));
	return tint * s;
}

float shooting_star(vec2 uv, float t) {
	float cycle = 13.0;
	float k = floor(t / cycle);
	float ct = mod(t, cycle);
	float dur = 1.1;
	if (ct > dur) {
		return 0.0;
	}
	float p = ct / dur;
	vec2 start = vec2((0.1 + 0.5 * hash21(vec2(k, 1.0))) * aspect, 0.04 + 0.3 * hash21(vec2(k, 2.0)));
	vec2 dir = normalize(vec2(1.0, 0.5));
	vec2 head = start + dir * p * 0.7;
	vec2 d = uv - head;
	float behind = -dot(d, dir);
	float side = abs(dot(d, vec2(-dir.y, dir.x)));
	float trail = clamp(1.0 - behind / 0.22, 0.0, 1.0);
	float visible = step(0.0, behind) * trail * smoothstep(0.004, 0.0, side);
	return visible * sin(p * 3.14159);
}

void fragment() {
	vec2 uv = vec2(UV.x * aspect, UV.y);
	float t = TIME;

	vec3 col = mix(deep_a.rgb, deep_b.rgb, UV.y * 0.9 + UV.x * 0.2);

	vec2 drift = vec2(t * 0.006, t * 0.002);
	float n1 = fbm(uv * 1.7 + drift + vec2(2.0, 5.0));
	float n2 = fbm(uv * 2.6 - drift * 1.7 + vec2(11.0, 3.0));
	float n3 = fbm(uv * 3.4 + vec2(-t * 0.004, t * 0.003) + vec2(7.0, 1.0));

	float cloud_a = pow(smoothstep(0.38, 0.85, n1), 1.8);
	float cloud_b = pow(smoothstep(0.40, 0.88, n2), 1.9);
	float cloud_c = pow(smoothstep(0.52, 0.90, n3), 2.4);
	col += violet.rgb * cloud_a * 0.55;
	col += teal.rgb * cloud_b * 0.45;
	col += rose.rgb * cloud_c * 0.22;

	col += star_layer(uv + vec2(t * 0.0015, 0.0), 38.0, 0.16, t, 1.0) * 0.55;
	col += star_layer(uv + vec2(t * 0.0030, 0.0), 70.0, 0.14, t * 1.2, 5.0) * 0.8;
	col += star_layer(uv + vec2(t * 0.0055, 0.0), 120.0, 0.12, t * 1.5, 9.0) * 1.0;

	col += vec3(0.9, 0.95, 1.0) * shooting_star(uv, t) * 0.9;

	COLOR = vec4(col, 1.0);
}
";

	private const string PlanetShader = @"
shader_type canvas_item;

uniform vec4 base_a : source_color = vec4(0.16, 0.30, 0.55, 1.0);
uniform vec4 base_b : source_color = vec4(0.62, 0.40, 0.55, 1.0);
uniform vec4 atmosphere : source_color = vec4(0.40, 0.72, 1.0, 1.0);
uniform vec2 light_dir = vec2(-0.7, 0.55);

// Integer hash: float hashes with fract() give different results for the same input when the GPU compiler
// reorders the arithmetic, which showed up as hard edges in the clouds.
float hash21(vec2 p) {
	uvec2 q = floatBitsToUint(p);
	uint h = q.x * 1597334673u + q.y * 3812015801u;
	h ^= h >> 16u;
	h *= 2146121005u;
	h ^= h >> 15u;
	h *= 2221713035u;
	h ^= h >> 16u;
	return float(h) * 2.3283064e-10;
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), f.x),
		mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 5; i++) {
		v += a * noise(p);
		p = p * 2.1 + vec2(13.0, 7.0);
		a *= 0.5;
	}
	return v;
}

void fragment() {
	vec2 p = (UV - 0.5) * 2.0 * 1.35;
	float r = length(p);
	float inside = smoothstep(1.0, 0.99, r);

	float z = sqrt(max(0.0, 1.0 - r * r));
	vec3 n = vec3(p, z);
	vec3 l = normalize(vec3(light_dir, 0.65));
	float diffuse = clamp(dot(n, l), 0.0, 1.0);

	float lon = p.x / max(z, 0.25);
	float bands = fbm(vec2(lon * 0.9 + TIME * 0.012, p.y * 4.5));
	float swirl = fbm(vec2(lon * 2.4 - TIME * 0.02, p.y * 9.0 + bands * 2.0));
	vec3 surface = mix(base_a.rgb, base_b.rgb, smoothstep(0.30, 0.75, bands));
	surface = mix(surface, surface * 1.25 + 0.04, swirl * 0.5);

	vec3 lit = surface * (diffuse * 1.15 + 0.035);
	float rim = pow(1.0 - z, 3.0);
	lit += atmosphere.rgb * rim * (0.25 + 0.9 * diffuse);

	float halo = smoothstep(1.35, 1.0, r) * (1.0 - inside);
	vec3 halo_col = atmosphere.rgb * halo * halo * (0.22 + 0.5 * clamp(dot(normalize(p + 0.0001), light_dir), 0.0, 1.0));

	COLOR = vec4(lit * inside + halo_col, max(inside, halo * halo * 0.6));
}
";

	private ShaderMaterial _spaceMaterial;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_spaceMaterial = new ShaderMaterial { Shader = new Shader { Code = SpaceShader } };
		var space = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Material = _spaceMaterial };
		space.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(space);

		// A planet hanging in the upper right corner, partly out of the picture.
		var planet = new ColorRect
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Color = Colors.White,
			Material = new ShaderMaterial { Shader = new Shader { Code = PlanetShader } }
		};
		planet.AnchorLeft = 1f;
		planet.AnchorRight = 1f;
		planet.AnchorTop = 0f;
		planet.AnchorBottom = 0f;
		planet.OffsetLeft = -330;
		planet.OffsetRight = 150;
		planet.OffsetTop = -190;
		planet.OffsetBottom = 290;
		AddChild(planet);

		Resized += UpdateAspect;
		UpdateAspect();
	}

	private void UpdateAspect()
	{
		if (_spaceMaterial != null && Size.Y > 0)
			_spaceMaterial.SetShaderParameter("aspect", Size.X / Size.Y);
	}
}
