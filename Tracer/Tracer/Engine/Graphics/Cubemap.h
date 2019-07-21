#pragma once

#include <string>
#include <vector>
#include <glad/glad.h>
#include <glm/glm.hpp>
#include <SFML/Graphics.hpp>

// Temporary define
#define AnisotropicFiltering 4

namespace marcher {
	class Cubemap {
	public:
		Cubemap();

		void Create(std::vector<std::string> files);

		void Bind(int unit = -1);
		GLuint GetHandle() { return m_handle; }

		~Cubemap();

	private:
		GLuint m_handle;
	};
}