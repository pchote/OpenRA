#version 140

uniform vec3 Scroll;
uniform vec3 r1, r2;

in vec4 aVertexPosition;
in vec3 aVertexTexCoord;
in uint aVertexTexFlags;
out vec3 vTexCoord;

void main()
{
	float vTexLayer = ((aVertexTexFlags >> 8) & 0xFFU);

	gl_Position = vec4((aVertexPosition.xyz - Scroll.xyz) * r1 + r2, 1);
	vTexCoord = vec3(aVertexTexCoord.st, vTexLayer);
} 
