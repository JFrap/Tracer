#include "Texture.h"

namespace marcher {
	Texture::Texture(GLenum format, bool linear, GLenum type, GLenum internalFormat) : format(format), linear(linear), type(type), internalFormat(internalFormat), m_handle(0) {
		glGenTextures(1, &m_handle);
	}

	void Texture::Create(glm::vec2 size) {
		Bind();
		glTexImage2D(GL_TEXTURE_2D, 0, internalFormat, size.x, size.y, 0, format, type, NULL);
		if (linear) {
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
		}
		else {
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
		}

		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, wrap);
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, wrap);
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_R, wrap);

		glBindTexture(GL_TEXTURE_2D, 0);
	}

	void Texture::Bind(int unit) {
		if (unit >= 0)
			glActiveTexture(GL_TEXTURE0 + unit);
		glBindTexture(GL_TEXTURE_2D, m_handle);
	}

	void Texture::Resize(glm::vec2 s) {
		Bind();
		size = s;
		glTexImage2D(GL_TEXTURE_2D, 0, internalFormat, size.x, size.y, 0, format, type, NULL);
	}

	Texture::~Texture() {
		glDeleteTextures(1, &m_handle);
	}
}