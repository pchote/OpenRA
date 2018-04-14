#version 140

uniform sampler2DArray DiffuseTexture;
uniform sampler2DArray Palette;

uniform bool EnableDepthPreview;
uniform float DepthTextureScale;

in vec3 vTexCoord;
in vec3 vDepthCoord;
in float vTexPalette;
in vec4 vChannelMask;
in vec4 vDepthMask;
out vec4 fragColor;

float jet_r(float x)
{
	return x < 0.7 ? 4.0 * x - 1.5 : -4.0 * x + 4.5;
}

float jet_g(float x)
{
	return x < 0.5 ? 4.0 * x - 0.5 : -4.0 * x + 3.5;
}

float jet_b(float x)
{
	return x < 0.3 ? 4.0 * x + 0.5 : -4.0 * x + 2.5;
}

void main()
{
	vec4 x = texture(DiffuseTexture, vTexCoord);
	vec3 p = vec3(dot(x, vChannelMask), vTexPalette, 0);
	vec4 c = texture(Palette, p);

	// Discard any transparent fragments (both color and depth)
	if (c.a == 0.0)
		discard;

	float depth = gl_FragCoord.z;
	if (length(vDepthMask) > 0.0)
	{
		vec4 y = texture(DiffuseTexture, vDepthCoord);
		depth = depth + DepthTextureScale * dot(y, vDepthMask);
	}

	// Convert to window coords
	gl_FragDepth = 0.5 * depth + 0.5;

	if (EnableDepthPreview)
	{
		float x = 1.0 - gl_FragDepth;
		float r = clamp(jet_r(x), 0.0, 1.0);
		float g = clamp(jet_g(x), 0.0, 1.0);
		float b = clamp(jet_b(x), 0.0, 1.0);
		fragColor = vec4(r, g, b, 1.0);
	}
	else
		fragColor = c;
}
