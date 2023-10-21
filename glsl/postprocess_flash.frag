#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform float Frac;
uniform vec3 Color;
uniform sampler2D WorldTexture;

#if __VERSION__ == 120
uniform vec2 WorldTextureSize;
#else
out vec4 fragColor;
#endif

void main()
{
#if __VERSION__ == 120
	vec4 c = texture2D(WorldTexture, gl_FragCoord.xy / WorldTextureSize);
#else
	vec4 c = texture(WorldTexture, gl_FragCoord.xy / textureSize(WorldTexture, 0));
#endif

	c = vec4(Color, c.a) * Frac + c * (1.0 - Frac);

	#if __VERSION__ == 120
	gl_FragColor = c;
	#else
	fragColor = c;
	#endif
}
