#include "Framebuffer.h"

namespace marcher {
	Framebuffer::Framebuffer(glm::uvec2 size) : m_size(size) {
		glGenFramebuffers(1, &m_handle);
	}

	void Framebuffer::AddTexture(GLenum attachment, GLenum format, GLenum internalFormat, GLenum type) {
		if (m_attachments.find(attachment) == m_attachments.end()) {
			Bind();
			m_attachments[attachment] = Texture(format, true, type, internalFormat);
			m_attachments[attachment].Create(m_size);
			m_attachments[attachment].Bind();

			glFramebufferTexture2D(GL_FRAMEBUFFER, attachment, GL_TEXTURE_2D, m_attachments[attachment].GetHandle(), 0);
			UnBind();
		}
	}

	void Framebuffer::Resize(glm::uvec2 newSize) {
		for (auto &attachment : m_attachments) {
			attachment.second.Resize(newSize);
		}
		m_size = newSize;
	}

	Texture& Framebuffer::GetTexture(GLenum attachment) {
		if (m_attachments.find(attachment) != m_attachments.end()) {
			return m_attachments[attachment];
		}
		return m_attachments[GL_COLOR_ATTACHMENT0];
	}

	Framebuffer::~Framebuffer() {
		glDeleteFramebuffers(1, &m_handle);
	}
}