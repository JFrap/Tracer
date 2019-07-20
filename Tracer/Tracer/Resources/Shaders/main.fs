#version 330 core
out vec4 FragColor;

in vec2 TexCoords;

#define EPSILON 0.0001f
#define MAX_DISTANCE 100

#define BOUNCES 8
#define SAMPLES 2

uniform sampler2D AccumulationTexture;
uniform bool Changed;
uniform int CurrentSample;

struct Camera {
    vec3 Position, Target;
    vec3 TopLeft, TopRight, BottomLeft, BottomRight;
};

struct Ray {
    vec3 Origin, Direction;
    vec3 Energy;
};

uniform vec2 ScreenSize;
uniform Camera MainCamera;
uniform float Time;
uniform float uSeed;
float Seed;

struct Sphere {
    vec3 Position;
    float Radius;
    vec3 Albedo, Specular;
    float Smoothness;
    vec3 Emission;
};

Sphere CreateSphere(vec3 position = vec3(0), float radius = 1, vec3 albedo = vec3(1), vec3 specular = vec3(.5), float smoothness = .5, vec3 emission = vec3(0)) {
    return Sphere(position, radius, albedo, specular, smoothness, emission);
}

Sphere CreateLightSphere(vec3 position = vec3(0), float radius = 1, vec3 emission = vec3(0)) {
    return Sphere(position, radius, vec3(0), vec3(0), 0, emission);
}

#define NUM_SPHERES 7
Sphere spheres[NUM_SPHERES];

const vec3 BackgroundColor = vec3(.2);

float hash1() {
    return fract(sin(Seed += 0.1)*43758.5453123);
}

vec2 hash2() {
    return fract(sin(vec2(Seed+=0.1,Seed+=0.1))*vec2(43758.5453123,22578.1459123));
}

vec3 hash3() {
    return fract(sin(vec3(Seed+=0.1,Seed+=0.1,Seed+=0.1))*vec3(43758.5453123,22578.1459123,19642.3490423));
}

vec3 cosWeightedRandomHemisphereDirection(const vec3 n) {
  	vec2 r = hash2();
    
	vec3  uu = normalize( cross( n, vec3(0.0,1.0,1.0) ) );
	vec3  vv = cross( uu, n );
	
	float ra = sqrt(r.y);
	float rx = ra*cos(6.2831*r.x); 
	float ry = ra*sin(6.2831*r.x);
	float rz = sqrt( 1.0-r.y );
	vec3  rr = vec3( rx*uu + ry*vv + rz*n );
    
    return normalize(rr);
}

vec3 randomSphereDirection() {
    vec2 h = hash2() * vec2(2.,6.28318530718)-vec2(1,0);
    float phi = h.y;
	return vec3(sqrt(1.-h.x*h.x)*vec2(sin(phi),cos(phi)),h.x);
}

vec3 randomHemisphereDirection(const vec3 n) {
	vec3 dr = randomSphereDirection();
	return dot(dr,n) * dr;
}

mat3 GetTangentSpace(vec3 normal) {
    // Choose a helper vector for the cross product
    vec3 helper = vec3(1, 0, 0);
    if (abs(normal.x) > 0.99f)
        helper = vec3(0, 0, 1);

    // Generate vectors
    vec3 tangent = normalize(cross(normal, helper));
    vec3 binormal = normalize(cross(normal, tangent));
    return mat3(tangent, binormal, normal);
}


vec3 randomHemisphereDirection(const vec3 normal, in float alpha) {
    // Sample the hemisphere, where alpha determines the kind of the sampling
    float cosTheta = pow(hash1(), 1.0f / (alpha + 1.0f));
    float sinTheta = sqrt(1.0f - cosTheta * cosTheta);
    float phi = 6.28318530718 * hash1();
    vec3 tangentSpaceDir = vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);

    // Transform direction to world space
    return GetTangentSpace(normal) * tangentSpaceDir;
}

float energy(vec3 color) {
    return dot(color, vec3(1.0f / 3.0f));
}

float sdot(vec3 x, vec3 y, float f = 1.0f) {
    return clamp(dot(x, y) * f, 0, 1);
}

float SmoothnessToPhongAlpha(float s) {
    return pow(1000.0f, s * s);
}

Ray CalculateFragRay() {
    vec2 RelScreenPos = gl_FragCoord.xy / ScreenSize;

    vec3 TopPos = mix(MainCamera.TopLeft, MainCamera.TopRight, RelScreenPos.x);
    vec3 BottomPos = mix(MainCamera.BottomLeft, MainCamera.BottomRight, RelScreenPos.x);
    vec3 FinalPos = mix(TopPos, BottomPos, RelScreenPos.y);
    return Ray(FinalPos, normalize(FinalPos-MainCamera.Position), vec3(1.f));
}

struct HitResult {
    vec3 Position, Normal;
    float Distance;
    bool Hit;
};

HitResult IntersectSphere(in Ray ray, in vec4 sphere) {
    vec3 L = sphere.xyz - ray.Origin; 
    float tca = dot(L, ray.Direction);
    float d2 = dot(L, L) - tca * tca; 
    float radius2 = sphere.w * sphere.w;
    if (d2 > radius2) 
        return HitResult(vec3(0), vec3(0), MAX_DISTANCE, false);
    float thc = sqrt(radius2 - d2);
    vec2 t = vec2(tca - thc, tca + thc);

    vec3 hitPos = ray.Origin + ray.Direction * t.x;
    return HitResult(hitPos, normalize(hitPos - sphere.xyz), t.x, true);
}

struct TraceResult {
    HitResult Result;
    int HitSphere, HitLight;
};

TraceResult Trace(in Ray ray) {
    float mint = MAX_DISTANCE;
    TraceResult retRes = TraceResult(HitResult(vec3(0), vec3(0), 0, false), -1, -1);
    for (int i = 0; i < NUM_SPHERES; i++) {
        HitResult current = IntersectSphere(ray, vec4(spheres[i].Position, spheres[i].Radius));
        if (current.Hit && current.Distance > 0 && current.Distance < mint) {
            retRes.Result = current;
            retRes.HitSphere = i;
            retRes.HitLight = -1;
            mint = current.Distance;
        }
    }
    return retRes;
}

TraceResult TraceLight(in Ray ray) {
    float mint = MAX_DISTANCE;
    TraceResult retRes = TraceResult(HitResult(vec3(0), vec3(0), 0, false), -1, -1);
    for (int i = 0; i < NUM_SPHERES; i++) {
        HitResult current = IntersectSphere(ray, vec4(spheres[i].Position, spheres[i].Radius));
        if (current.Hit && current.Distance > 0 && current.Distance < mint) {
            retRes.Result = current;
            retRes.HitSphere = i;
            retRes.HitLight = -1;
            mint = current.Distance;
        }
    }
    return retRes;
}

vec3 Shade(inout Ray ray, in TraceResult result) {
    if (result.Result.Hit) {
        vec3 alb = min(1.0f - spheres[result.HitSphere].Specular, spheres[result.HitSphere].Albedo);
        float specChance = energy(spheres[result.HitSphere].Specular);
        float diffChance = energy(spheres[result.HitSphere].Albedo);
        float roulette = hash1();

        HitResult hit = result.Result;
        
        if (roulette < specChance) {
            ray.Origin = hit.Position + hit.Normal * 0.001f;
            float alpha = SmoothnessToPhongAlpha(spheres[result.HitSphere].Smoothness);
            ray.Direction = randomHemisphereDirection(reflect(ray.Direction, hit.Normal), alpha);
            float f = (alpha + 2) / (alpha + 1);
            ray.Energy *= (1.0f / specChance) * spheres[result.HitSphere].Specular * sdot(hit.Normal, ray.Direction, f);
        }
        else if (diffChance > 0 && roulette < specChance + diffChance) {
            ray.Origin = hit.Position + hit.Normal * 0.001f;
            ray.Direction = randomHemisphereDirection(hit.Normal, 1.0f);
            ray.Energy *= (1.0f / diffChance) * spheres[result.HitSphere].Albedo; 
        }
        else {
            ray.Energy = vec3(0.0f);
        }

        return spheres[result.HitSphere].Emission;
    }

    ray.Energy = vec3(0);
    return BackgroundColor;
}

vec3 Render(in Ray ray) {
    TraceResult info = TraceLight(ray);

    if (info.Result.Hit) {
        

        vec3 finalCol = vec3(0);
        if (info.HitLight != -1)
            return vec3(1);
        else {
            for (int i = 0; i < BOUNCES; i++) {
                vec3 OldEnergy = ray.Energy;
                finalCol += OldEnergy * Shade(ray, info);
                if (ray.Energy == vec3(0)) {
                    break;
                }
                info = TraceLight(ray);
            }
        }
        
        return finalCol;
    }
    return BackgroundColor;
}

void main() {
    vec2 p = -1.0 + 2.0 * (gl_FragCoord.xy) / ScreenSize.xy;
    p.x *= ScreenSize.x/ScreenSize.y;

    Seed = p.x + p.y * 3.43121412313 + fract(1.12345314312*Time*uSeed);
    
    //lights[0] = CreateLightSphere(vec3(10, 30, 10), 2, vec3(10));

    spheres[0] = CreateLightSphere(vec3(5, 5, 5), .5, vec3(800));
    spheres[1] = CreateLightSphere(vec3(-5, 5, 5), .5, vec3(800));
    spheres[2] = CreateSphere(vec3(0, -50, 0), 50, vec3(.8), vec3(.5), .2);
    spheres[3] = CreateSphere(vec3(3, 2, 3), 2, vec3(.8, .2, .2), vec3(0.8), 1.2);
    spheres[4] = CreateSphere(vec3(-3, 2, 3), 2, vec3(.8, .2, .2), vec3(0.6), 0.8);
    spheres[5] = CreateSphere(vec3(-3, 2, -3), 2, vec3(.8, .2, .2), vec3(0.4), 0.4);
    spheres[6] = CreateSphere(vec3(3, 2, -3), 2, vec3(.8, .2, .2), vec3(0.2), 0);
    
    
    Ray CamRay = CalculateFragRay();

    vec3 FinalColor = vec3(0);
    for (int i = 0; i < SAMPLES; i++) {
        Seed += 1;
        FinalColor += Render(CamRay);
    }
    FinalColor *= vec3(1.f/SAMPLES);
    FinalColor = pow(FinalColor, vec3(1.0/2.2));

    if (!Changed) {
        FinalColor = mix(texture(AccumulationTexture, TexCoords).rgb, FinalColor,  1.f/(CurrentSample));
    }

    FragColor = vec4(FinalColor, 1);
}