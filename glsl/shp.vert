#version 140

uniform vec3 Scroll;
uniform vec3 r1, r2;

in vec4 aVertexPosition;
in vec4 aVertexTexCoord;
in float aVertexTexPalette;
in uint aVertexTexFlags;
out vec3 vTexCoord;
out vec3 vDepthCoord;
out float vTexPalette;
out vec4 vChannelMask;
out vec4 vDepthMask;

vec4 DecodeChannelMask(uint x)
{
	if ((x & 0x07U) == 0x07U)
		return vec4(0,0,0,1);
	if ((x & 0x05U) == 0x05U)
		return vec4(0,0,1,0);
	if ((x & 0x03U) == 0x03U)
		return vec4(0,1,0,0);
	if ((x & 0x01U) == 0x01U)
		return vec4(1,0,0,0);
	return vec4(0,0,0,0);
}

void main()
{
	float vTexLayer = ((aVertexTexFlags >> 8) & 0xFFU);
	float vDepthLayer = ((aVertexTexFlags >> 16) & 0xFFU);

	gl_Position = vec4((aVertexPosition.xyz - Scroll.xyz) * r1 + r2, 1);
	vTexCoord = vec3(aVertexTexCoord.st, vTexLayer);
	vDepthCoord = vec3(aVertexTexCoord.pq, vDepthLayer);
	vTexPalette = aVertexTexPalette;
	vChannelMask = DecodeChannelMask(aVertexTexFlags);
	vDepthMask = DecodeChannelMask(aVertexTexFlags >> 3);
} 
