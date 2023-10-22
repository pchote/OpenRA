#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform float Frac;
uniform vec3 Color;
uniform sampler2D WorldTexture;
out vec4 fragColor;

void main()
{
	vec4 c = texture(WorldTexture, gl_FragCoord.xy / textureSize(WorldTexture, 0));
	fragColor = vec4(Color, c.a) * Frac + c * (1.0 - Frac);
}
