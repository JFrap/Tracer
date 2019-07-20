#pragma once

#include <glad/glad.h>
#include <glm/glm.hpp>

// Temporary define
#define AnisotropicFiltering 4

namespace marcher {
	class Texture {
	public:
		Texture(GLenum format = GL_RGBA, bool linear = true, GLenum type = GL_UNSIGNED_BYTE, GLenum internalFormat = GL_RGBA);

		void Create(glm::vec2 size);

		void Bind(int unit = -1);
		void Resize(glm::vec2 s);
		GLuint GetHandle() { return m_handle; }

		~Texture();

		glm::vec2 size = glm::vec2(0);

		GLenum format, internalFormat, type, wrap = GL_REPEAT;
		bool linear;
	private:
		GLuint m_handle;
	};
}