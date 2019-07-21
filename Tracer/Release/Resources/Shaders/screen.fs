#version 330 core
out vec4 FragColor;
  
in vec2 TexCoords;

uniform sampler2D screenTexture;

void main() {
    vec4 FinalColor = texture(screenTexture, TexCoords);
    FinalColor.rgb = pow(FinalColor.rgb, vec3(1.0/2.2));
    FragColor = FinalColor;
}