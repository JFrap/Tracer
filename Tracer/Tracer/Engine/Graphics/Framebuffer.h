#pragma once

#include <glad/glad.h>
#include <map>
#include "../Maths.h"

#include "Texture.h"

namespace marcher {
	class Framebuffer {
	public:
		Framebuffer(glm::uvec2 size);

		void AddTexture(GLenum attachment, GLenum format = GL_RGB, GLenum internalFormat = GL_RGB32F, GLenum type = GL_FLOAT);

		void Clear() { glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT); };
		void Bind() { glBindFramebuffer(GL_FRAMEBUFFER, m_handle); }
		void Viewport() { glViewport(0, 0, m_size.x, m_size.y); }
		static void UnBind() { glBindFramebuffer(GL_FRAMEBUFFER, 0); }

		void Resize(glm::uvec2 newSize);

		glm::uvec2 GetSize() { return m_size; };
		Texture& GetTexture(GLenum attachment);

		~Framebuffer();
	private:
		glm::uvec2 m_size;
		std::map<GLenum, Texture> m_attachments;
		GLuint m_handle;
	};
}