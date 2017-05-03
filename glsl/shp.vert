#version 140
uniform vec3 Scroll;
uniform vec3 r1, r2;

in vec4 aVertexPosition;
in vec4 aVertexTexCoord;
in vec2 aVertexTexMetadata;
out vec4 vTexCoord;
out vec2 vTexMetadata;
out vec4 vChannelMask;
out vec4 vDepthMask;

vec4 DecodeChannelMask(float x)
{
	if (x > 0.7)
		return vec4(0,0,0,1);
	if (x > 0.5)
		return vec4(0,0,1,0);
	if (x > 0.3)
		return vec4(0,1,0,0);
	else
		return vec4(1,0,0,0);
}

void main()
{
	gl_Position = vec4((aVertexPosition.xyz - Scroll.xyz) * r1 + r2, 1);
	vTexCoord = aVertexTexCoord;
	vTexMetadata = aVertexTexMetadata;
	vChannelMask = DecodeChannelMask(abs(aVertexTexMetadata.t));
	if (aVertexTexMetadata.t < 0.0)
	{
		float x = -aVertexTexMetadata.t * 10.0;
		vDepthMask = DecodeChannelMask(x - floor(x));
	}
	else
		vDepthMask = vec4(0,0,0,0);
} 
