#include <SFML/Graphics.hpp>
#include <glad/glad.h>

#include "Engine/Graphics/Shader.h"
#include "Engine/Graphics/Camera.h"
#include "Engine/Timer.h"
#include "Engine/Graphics/Framebuffer.h"
#include "Engine/Graphics/Cubemap.h"

int main() {
	srand(time(0));
	sf::ContextSettings settings;
	settings.depthBits = 24;
	settings.stencilBits = 8;
	settings.majorVersion = 3;
	settings.minorVersion = 3;

	sf::Window window(sf::VideoMode(1280, 720), "Tracer", sf::Style::Default);

	//window.setFramerateLimit(30);

	int gladInitRes = gladLoadGL();
	if (!gladInitRes) {
		fprintf(stderr, "Unable to initialize glad\n");
		window.close();
		return -1;
	}

	std::shared_ptr<marcher::Shader> mainShader = std::unique_ptr<marcher::Shader>(new marcher::Shader("main.vs", "main.fs"));
	std::shared_ptr<marcher::Shader> accumulationShader = std::unique_ptr<marcher::Shader>(new marcher::Shader("accumulation.vs", "accumulation.fs"));
	std::shared_ptr<marcher::Shader> screenShader = std::unique_ptr<marcher::Shader>(new marcher::Shader("screen.vs", "screen.fs"));

	float vertices[] = {
		-1.f, -1.f, 0.f,
		-1.f,  1.f, 0.f,
		 1.f,  1.f, 0.f,
		 1.f,  1.f, 0.f,
		 1.f, -1.f, 0.f,
		-1.f, -1.f, 0.f
	};

	unsigned int VBO, VAO;
	glGenVertexArrays(1, &VAO);
	glGenBuffers(1, &VBO);
	glBindVertexArray(VAO);

	glBindBuffer(GL_ARRAY_BUFFER, VBO);
	glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

	glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 3 * sizeof(float), (void*)0);
	glEnableVertexAttribArray(0);

	glBindBuffer(GL_ARRAY_BUFFER, 0);
	glBindVertexArray(0);

	marcher::Framebuffer buffer = marcher::Framebuffer({ window.getSize().x, window.getSize().y });
	buffer.AddTexture(GL_COLOR_ATTACHMENT0);

	marcher::Framebuffer accumulationBuffer = marcher::Framebuffer({ window.getSize().x, window.getSize().y });
	accumulationBuffer.AddTexture(GL_COLOR_ATTACHMENT0);

	marcher::Cubemap map;
	map.Create({"00/px.png", "00/nx.png", "00/py.png", "00/ny.png", "00/pz.png", "00/nz.png", });

	glm::vec3 cameraRot = glm::vec3();
	marcher::Camera camera = marcher::Camera(glm::vec3(0, 4, 4), glm::vec3(0, 0, 0));

	marcher::Timer frameTimer, timer;
	unsigned int TotalFrames = 0;
	float totalTime = 0;

	bool active = true;
	bool changed = true;
	unsigned int currentSample = 0;
	while (window.isOpen()) {
		float dt = timer.Restart<float>();
		totalTime += dt;
		
		sf::Event event;
		while (window.pollEvent(event)) {
			if (event.type == sf::Event::Closed)
				window.close();
			else if (event.type == sf::Event::Resized) {
				buffer.Resize({ window.getSize().x, window.getSize().y });
				accumulationBuffer.Resize({ window.getSize().x, window.getSize().y });
			}
			else if (event.type == sf::Event::KeyPressed) {
				if (event.key.code == sf::Keyboard::F1) {
					printf("Reloading Shaders...\n");
					mainShader = std::unique_ptr<marcher::Shader>(new marcher::Shader("main.vs", "main.fs"));
					accumulationShader = std::unique_ptr<marcher::Shader>(new marcher::Shader("accumulation.vs", "accumulation.fs"));
					screenShader = std::unique_ptr<marcher::Shader>(new marcher::Shader("screen.vs", "screen.fs"));
				}
			}
			else if (event.type == sf::Event::LostFocus) {
				active = false;
			}
			else if (event.type == sf::Event::GainedFocus) {
				active = true;
			}
		}

		TotalFrames++;
		float currentTime = frameTimer.CurrentTime<float>();
		if (currentTime >= 1.f) {
			printf("%f FPS | %f MS | %i Samples \n", (float)TotalFrames / currentTime, (currentTime / (float)TotalFrames) * 1000, currentSample);

			frameTimer.Restart();
			TotalFrames = 0;
		}

		sf::Vector2i windowCenter = sf::Vector2i(window.getSize().x / 2, window.getSize().y / 2);
		if (sf::Mouse::getPosition(window) != windowCenter && active) {
			cameraRot.y += ((float)sf::Mouse::getPosition(window).x - (float)windowCenter.x) / 1000;
			cameraRot.x += ((float)sf::Mouse::getPosition(window).y - (float)windowCenter.y) / 1000;
			sf::Mouse::setPosition(windowCenter, window);
			changed = true;
		}

		auto camDir = glm::vec3(glm::vec4(0, 0, -1, 1) * glm::rotate(cameraRot.x, glm::vec3(1, 0, 0)) * glm::rotate(cameraRot.y, glm::vec3(0, 1, 0)));
		auto camRight = glm::vec3(glm::vec4(1, 0, 0, 1) * glm::rotate(cameraRot.y, glm::vec3(0, 1, 0)));

		float speed = 5.f;

		if (active) {
			if (sf::Keyboard::isKeyPressed(sf::Keyboard::Space)) {
				speed = 20;
			}

			if (sf::Keyboard::isKeyPressed(sf::Keyboard::W)) {
				camera.Position += camDir * dt * speed;
				changed = true;
			}

			if (sf::Keyboard::isKeyPressed(sf::Keyboard::S)) {
				camera.Position -= camDir * dt * speed;
				changed = true;
			}

			if (sf::Keyboard::isKeyPressed(sf::Keyboard::D)) {
				camera.Position += camRight * dt * speed;
				changed = true;
			}

			if (sf::Keyboard::isKeyPressed(sf::Keyboard::A)) {
				camera.Position -= camRight * dt * speed;
				changed = true;
			}

			if (sf::Keyboard::isKeyPressed(sf::Keyboard::LShift)) {
				camera.Position += glm::vec3(0, 1, 0) * dt * speed;
				changed = true;
			}

			if (sf::Keyboard::isKeyPressed(sf::Keyboard::LControl)) {
				camera.Position -= glm::vec3(0, 1, 0) * dt * speed;
				changed = true;
			}
		}

		camera.Target = camera.Position + camDir;

		glBindTexture(GL_TEXTURE_2D, 0);
		buffer.Bind();
		buffer.Viewport();
		buffer.Clear();

		mainShader->Bind();
		camera.Update(mainShader, (float)window.getSize().x / (float)window.getSize().y, glm::vec4(0, 0, window.getSize().x, window.getSize().y));
		mainShader->SendUniform("ScreenSize", glm::vec2(window.getSize().x, window.getSize().y));
		mainShader->SendUniform("Time", totalTime);
		mainShader->SendUniform("uSeed", static_cast <float> (rand()) / static_cast <float> (RAND_MAX));
		

		map.Bind(0);
		mainShader->SendUniform("EnviromentTexture", 0);

		mainShader->SendUniform("AccumulationTexture", 1);
		accumulationBuffer.GetTexture(GL_COLOR_ATTACHMENT0).Bind(1);

		mainShader->SendUniform("Changed", (int)changed);
		if (changed) {
			currentSample = 0;
			changed = false;
		}
		currentSample++;
		mainShader->SendUniform("CurrentSample", (int)currentSample);

		glBindVertexArray(VAO); 
		glDrawArrays(GL_TRIANGLES, 0, 6);


		glBindTexture(GL_TEXTURE_CUBE_MAP, 0);
		accumulationBuffer.Bind();
		accumulationBuffer.Viewport();
		accumulationBuffer.Clear();

		accumulationShader->Bind();

		accumulationShader->SendUniform("newTexture", 0);
		
		buffer.GetTexture(GL_COLOR_ATTACHMENT0).Bind(0);

		glDrawArrays(GL_TRIANGLES, 0, 6);


		glBindTexture(GL_TEXTURE_2D, 0);
		accumulationBuffer.UnBind();
		glViewport(0, 0, window.getSize().x, window.getSize().y);
		glClear(GL_COLOR_BUFFER_BIT);
		screenShader->Bind();
		screenShader->SendUniform("screenTexture", 0);
		accumulationBuffer.GetTexture(GL_COLOR_ATTACHMENT0).Bind(0);
		screenShader->SendUniform("ScreenSize", glm::vec2(window.getSize().x, window.getSize().y));

		glDrawArrays(GL_TRIANGLES, 0, 6);

		window.display();
	}
	
	glDeleteVertexArrays(1, &VAO);
	glDeleteBuffers(1, &VBO);

	return 0;
}