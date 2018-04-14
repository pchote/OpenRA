#version 140

uniform mat4 View;
uniform mat4 TransformMatrix;

in vec4 aVertexPosition;
in vec4 aVertexTexCoord;
in float aVertexTexPalette;
in uint aVertexTexFlags;
out vec4 vTexCoord;
out vec4 vChannelMask;
out vec4 vNormalsMask;

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
	gl_Position = View*TransformMatrix*aVertexPosition;
	vTexCoord = aVertexTexCoord;
	vChannelMask = DecodeChannelMask(aVertexTexFlags);
	vNormalsMask = DecodeChannelMask(aVertexTexFlags >> 3);
}
