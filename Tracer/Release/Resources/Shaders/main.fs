#version 330 core
out vec4 FragColor;

in vec2 TexCoords;

#define EPSILON 0.0001f
#define MAX_DISTANCE 100

#define BOUNCES 8
#define SAMPLES 1

uniform sampler2D AccumulationTexture;
uniform samplerCube EnviromentTexture;
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

struct Material {
    vec3 Albedo, Specular;
    float Smoothness;
    vec3 Emission;
};

struct Sphere {
    vec3 Position;
    float Radius;
    int MaterialID;
};

struct Plane {
    vec3 Position;
    vec3 Normal;
    int MaterialID;
};

uniform vec2 ScreenSize;
uniform Camera MainCamera;
uniform float Time;
uniform float uSeed;
float Seed;

const vec3 BackgroundColor = vec3(.2);

#define NUM_MATERIALS 5
Material materials[NUM_MATERIALS];

#define NUM_SPHERES 5
Sphere spheres[NUM_SPHERES];

#define NUM_PLANES 1
Plane planes[NUM_PLANES];

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
    vec3 helper = vec3(1, 0, 0);
    if (abs(normal.x) > 0.99f)
        helper = vec3(0, 0, 1);

    vec3 tangent = normalize(cross(normal, helper));
    vec3 binormal = normalize(cross(normal, tangent));
    return mat3(tangent, binormal, normal);
}


vec3 randomHemisphereDirection(const vec3 normal, in float alpha) {
    float cosTheta = pow(hash1(), 1.0f / (alpha + 1.0f));
    float sinTheta = sqrt(1.0f - cosTheta * cosTheta);
    float phi = 6.28318530718 * hash1();
    vec3 tangentSpaceDir = vec3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);

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
    RelScreenPos += (vec2(1.f)/ScreenSize)*hash2();

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

HitResult IntersectPlane(in Ray ray, in vec3 planePos, in vec3 planeNorm) {
    float d = dot(planePos, -planeNorm);
    float t = -(d + ray.Origin.z * planeNorm.z + ray.Origin.y * planeNorm.y + ray.Origin.x * planeNorm.x) / (ray.Direction.z * planeNorm.z + ray.Direction.y * planeNorm.y + ray.Direction.x * planeNorm.x);
    if (t > 0 && t < MAX_DISTANCE && dot(planeNorm, ray.Direction) < 0)
        return HitResult(ray.Origin + ray.Direction * t, planeNorm, t, true);
    return HitResult(vec3(0), vec3(0), MAX_DISTANCE, false);
}

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
    int HitSphere, HitPlane; // 0: Sphere, 1: Plane
};

TraceResult TraceLight(in Ray ray) {
    float mint = MAX_DISTANCE;
    TraceResult retRes = TraceResult(HitResult(vec3(0), vec3(0), 0, false), -1, -1);
    for (int i = 0; i < NUM_SPHERES; i++) {
        HitResult current = IntersectSphere(ray, vec4(spheres[i].Position, spheres[i].Radius));
        if (current.Hit && current.Distance > 0 && current.Distance < mint) {
            retRes.Result = current;
            retRes.HitSphere = i;
            retRes.HitPlane = -1;
            mint = current.Distance;
        }
    }

    for (int i = 0; i < NUM_PLANES; i++) {
        HitResult current = IntersectPlane(ray, planes[i].Position, planes[i].Normal);
        if (current.Hit && current.Distance > 0 && current.Distance < mint) {
            retRes.Result = current;
            retRes.HitSphere = -1;
            retRes.HitPlane = i;
            mint = current.Distance;
        }
    }

    return retRes;
}

vec3 GetBackground(vec3 dir) {
    //return texture(EnviromentTexture, dir).rgb;
    return vec3(0.2);
    //return mix(vec3(0.2, 0.8, 1), vec3(200), clamp(pow(dot(dir, normalize(vec3(0.5,1,0.5))), 500), 0, 1));
}

vec3 Shade(inout Ray ray, in TraceResult result) {
    if (result.Result.Hit) {
        int matid = 0;

        if (result.HitSphere >= 0)
            matid = spheres[result.HitSphere].MaterialID;
        else 
            matid = planes[result.HitPlane].MaterialID;

        Material mat = materials[matid];

        vec3 alb = min(1.0f - mat.Specular, mat.Albedo);
        float specChance = energy(mat.Specular);
        float diffChance = energy(mat.Albedo);
        float roulette = hash1();

        HitResult hit = result.Result;
        
        if (roulette < specChance) {
            ray.Origin = hit.Position + hit.Normal * 0.001f;
            float alpha = SmoothnessToPhongAlpha(mat.Smoothness);
            ray.Direction = randomHemisphereDirection(reflect(ray.Direction, hit.Normal), alpha);
            float f = (alpha + 2) / (alpha + 1);
            ray.Energy *= (1.0f / specChance) * mat.Specular * sdot(hit.Normal, ray.Direction, f);
        }
        else if (diffChance > 0 && roulette < specChance + diffChance) {
            ray.Origin = hit.Position + hit.Normal * 0.001f;
            ray.Direction = randomHemisphereDirection(hit.Normal, 1.0f);
            ray.Energy *= (1.0f / diffChance) * mat.Albedo; 
        }
        else {
            ray.Energy = vec3(0.0f);
        }

        return mat.Emission;
    }

    ray.Energy = vec3(0);
    return GetBackground(ray.Direction);
}

vec3 Render(in Ray ray) {
    TraceResult info = TraceLight(ray);

    if (info.Result.Hit) {
        

        vec3 finalCol = vec3(0);
        for (int i = 0; i < BOUNCES; i++) {
            vec3 OldEnergy = ray.Energy;
            finalCol += OldEnergy * Shade(ray, info);
            if (ray.Energy == vec3(0)) {
                break;
            }
            info = TraceLight(ray);
        }
        
        return finalCol;
    }
    return GetBackground(ray.Direction);
}

void main() {
    vec2 p = -1.0 + 2.0 * (gl_FragCoord.xy) / ScreenSize.xy;
    p.x *= ScreenSize.x/ScreenSize.y;

    Seed = p.x + p.y * 3.43121412313 + fract(1.12345314312*Time*uSeed);
    
    materials[0] = Material(vec3(1), vec3(0), 0, vec3(40)); //Light

    materials[1] = Material(vec3(.8), vec3(.4), .3, vec3(0)); //Sphere material
    materials[2] = Material(vec3(1), vec3(.2), .2, vec3(0)); //Cornell White
    materials[3] = Material(vec3(1,.2,.2), vec3(.2), .2, vec3(0)); //Cornell Red
    materials[4] = Material(vec3(.2,1,.2), vec3(.2), .2, vec3(0)); //Cornell Green


    spheres[0] = Sphere(vec3(0, 6, 0), .5, 0);
    
    spheres[1] = Sphere(vec3(2, 1, 2), 1, 1);
    spheres[2] = Sphere(vec3(-2, 1, 2), 1, 1);
    spheres[3] = Sphere(vec3(-2, 1, -2), 1, 1);
    spheres[4] = Sphere(vec3(2, 1, -2), 1, 1);
    
    planes[0] = Plane(vec3(0), vec3(0,1,0), 2);
    // planes[1] = Plane(vec3(0, 12, 0), vec3(0,-1,0), 2);
    // planes[2] = Plane(vec3(6, 0, 0), vec3(-1,0,0), 2);
    // planes[3] = Plane(vec3(-6, 0, 0), vec3(1,0,0), 2);
    // planes[4] = Plane(vec3(0, 0, 6), vec3(0,0,-1), 3);
    // planes[5] = Plane(vec3(0, 0, -6), vec3(0,0,1), 4);
    
    Ray CamRay = CalculateFragRay();

    vec3 FinalColor = vec3(0);
    for (int i = 0; i < SAMPLES; i++) {
        Seed += 1;
        FinalColor += Render(CamRay);
    }
    FinalColor *= vec3(1.f/SAMPLES);

    if (!Changed) {
        vec3 texCol = texture(AccumulationTexture, TexCoords).rgb;
        FinalColor = mix(texCol, FinalColor,  1.f/(CurrentSample+1));
    }

    FragColor = vec4(FinalColor, 1);
}