#include <algorithm>
#include <cmath>
#include <cstring>
#include <vector>

#include "Scene.h"

#include "Numeric/PCGSolver.h"

namespace asa
{
namespace
{
////////////////////////////////////////////////
// Add any reusable classes or functions HERE //
////////////////////////////////////////////////

int clampInt(const int v, const int a, const int b)
{
    return std::max(a, std::min(v, b));
}

float clampFloat(const float v, const float a, const float b)
{
    return std::max(a, std::min(v, b));
}

Vector2 clampPositionToDomain(const Grid2 &grid, const Vector2 &pos)
{
    const AABox2 domain = grid.getDomain();
    const Vector2 dx = grid.getDx();
    const float eps = 0.001f * std::min(dx.x, dx.y);

    return Vector2(clampFloat(pos.x, domain.minPosition.x + eps, domain.maxPosition.x - eps),
                   clampFloat(pos.y, domain.minPosition.y + eps, domain.maxPosition.y - eps));
}

Vector3 sampleBilerpVector3(const Array2<Vector3> &array, const Vector2 &uv)
{
    const Index2 size = array.getSize();

    const int i = (int)std::floor(uv.x);
    const int j = (int)std::floor(uv.y);

    const float tx = uv.x - (float)i;
    const float ty = uv.y - (float)j;

    const uint i0 = (uint)clampInt(i, 0, (int)size.x - 1);
    const uint i1 = (uint)clampInt(i + 1, 0, (int)size.x - 1);
    const uint j0 = (uint)clampInt(j, 0, (int)size.y - 1);
    const uint j1 = (uint)clampInt(j + 1, 0, (int)size.y - 1);

    return bilerp(array[Index2(i0, j0)], array[Index2(i1, j0)], array[Index2(i0, j1)], array[Index2(i1, j1)], tx, ty);
}

float sampleBilerpFloat(const Array2<float> &array, const Vector2 &uv)
{
    const Index2 size = array.getSize();

    const int i = (int)std::floor(uv.x);
    const int j = (int)std::floor(uv.y);

    const float tx = uv.x - (float)i;
    const float ty = uv.y - (float)j;

    const uint i0 = (uint)clampInt(i, 0, (int)size.x - 1);
    const uint i1 = (uint)clampInt(i + 1, 0, (int)size.x - 1);
    const uint j0 = (uint)clampInt(j, 0, (int)size.y - 1);
    const uint j1 = (uint)clampInt(j + 1, 0, (int)size.y - 1);

    return bilerp(array[Index2(i0, j0)], array[Index2(i1, j0)], array[Index2(i0, j1)], array[Index2(i1, j1)], tx, ty);
}

Vector2 getVelocity(const Grid2 &grid,
                    const Array2<float> &velocityX,
                    const Array2<float> &velocityY,
                    const Vector2 &pos)
{
    const float u = sampleBilerpFloat(velocityX, grid.getFaceIndexX(pos));
    const float v = sampleBilerpFloat(velocityY, grid.getFaceIndexY(pos));

    return Vector2(u, v);
}

void addParticleContributionFloat(Array2<float> &array, Array2<float> &weight, const Vector2 &uv, const float value)
{
    const Index2 size = array.getSize();

    const int i = (int)std::floor(uv.x);
    const int j = (int)std::floor(uv.y);

    const float tx = uv.x - (float)i;
    const float ty = uv.y - (float)j;

    const int ix[2] = {clampInt(i, 0, (int)size.x - 1), clampInt(i + 1, 0, (int)size.x - 1)};
    const int iy[2] = {clampInt(j, 0, (int)size.y - 1), clampInt(j + 1, 0, (int)size.y - 1)};
    const float wx[2] = {1.0f - tx, tx};
    const float wy[2] = {1.0f - ty, ty};

    for (uint y = 0; y < 2; ++y) {
        for (uint x = 0; x < 2; ++x) {
            const float w = wx[x] * wy[y];
            const Index2 id((uint)ix[x], (uint)iy[y]);
            array[id] += value * w;
            weight[id] += w;
        }
    }
}

void addParticleContributionVector3(Array2<Vector3> &array,
                                    Array2<float> &weight,
                                    const Vector2 &uv,
                                    const Vector3 &value)
{
    const Index2 size = array.getSize();

    const int i = (int)std::floor(uv.x);
    const int j = (int)std::floor(uv.y);

    const float tx = uv.x - (float)i;
    const float ty = uv.y - (float)j;

    const int ix[2] = {clampInt(i, 0, (int)size.x - 1), clampInt(i + 1, 0, (int)size.x - 1)};
    const int iy[2] = {clampInt(j, 0, (int)size.y - 1), clampInt(j + 1, 0, (int)size.y - 1)};
    const float wx[2] = {1.0f - tx, tx};
    const float wy[2] = {1.0f - ty, ty};

    for (uint y = 0; y < 2; ++y) {
        for (uint x = 0; x < 2; ++x) {
            const float w = wx[x] * wy[y];
            const Index2 id((uint)ix[x], (uint)iy[y]);
            array[id] += value * w;
            weight[id] += w;
        }
    }
}

////////////////////////////////////////////////
}  // namespace

// Init particles
void Fluid2::initParticles()
{
    const Index2 size = grid.getSize();
    const Vector2 dx = grid.getDx();
    const AABox2 domain = grid.getDomain();

    particles.resize(0);

    // 4 particles per cell
    const uint particlesPerSide = (uint)std::ceil(std::sqrt((float)Scene::particlesPerCell));

    for (uint j = 0; j < size.y; ++j) {
        for (uint i = 0; i < size.x; ++i) {
            uint count = 0;
            for (uint y = 0; y < particlesPerSide && count < Scene::particlesPerCell; ++y) {
                for (uint x = 0; x < particlesPerSide && count < Scene::particlesPerCell; ++x) {
                    const float fx = ((float)x + 0.5f) / (float)particlesPerSide;
                    const float fy = ((float)y + 0.5f) / (float)particlesPerSide;

                    const Vector2 pos(domain.minPosition.x + ((float)i + fx) * dx.x,
                                      domain.minPosition.y + ((float)j + fy) * dx.y);

                    particles.addParticle(pos, Vector2(0.0f, 0.0f), Vector3(0.0f, 0.0f, 0.0f));
                    count++;
                }
            }
        }
    }
}

// Advection
void Fluid2::fluidAdvection(const float dt)
{
    if (flipEnabled) {
        // Move particles using midpoint --> RK2 and the grid velocities.
        for (uint p = 0; p < particles.getSize(); ++p) {
            const Vector2 pos = particles.getPosition(p);
            const Vector2 v1 = getVelocity(grid, velocityX, velocityY, pos);
            const Vector2 midPos = clampPositionToDomain(grid, pos + v1 * (0.5f * dt));
            const Vector2 v2 = getVelocity(grid, velocityX, velocityY, midPos);
            const Vector2 newPos = clampPositionToDomain(grid, pos + v2 * dt);

            particles.setPosition(p, newPos);
        }

        // Using bilinear weights to bring back particles porperites to the grid
        inkRGB.clear();
        velocityX.clear();
        velocityY.clear();

        Array2<float> inkWeight;
        Array2<float> velocityXWeight;
        Array2<float> velocityYWeight;
        inkWeight.resize(inkRGB.getSize());
        velocityXWeight.resize(velocityX.getSize());
        velocityYWeight.resize(velocityY.getSize());

        for (uint p = 0; p < particles.getSize(); ++p) {
            const Vector2 pos = particles.getPosition(p);
            const Vector2 vel = particles.getVelocity(p);
            const Vector3 ink = particles.getInk(p);

            addParticleContributionVector3(inkRGB, inkWeight, grid.getCellIndex(pos), ink);
            addParticleContributionFloat(velocityX, velocityXWeight, grid.getFaceIndexX(pos), vel.x);
            addParticleContributionFloat(velocityY, velocityYWeight, grid.getFaceIndexY(pos), vel.y);
        }

        const float eps = 1e-6f;

        for (uint j = 0; j < inkRGB.getSize().y; ++j) {
            for (uint i = 0; i < inkRGB.getSize().x; ++i) {
                const Index2 id(i, j);
                if (inkWeight[id] > eps)
                    inkRGB[id] *= (1.0f / inkWeight[id]);
            }
        }

        for (uint j = 0; j < velocityX.getSize().y; ++j) {
            for (uint i = 0; i < velocityX.getSize().x; ++i) {
                const Index2 id(i, j);
                if (velocityXWeight[id] > eps)
                    velocityX[id] /= velocityXWeight[id];
            }
        }

        for (uint j = 0; j < velocityY.getSize().y; ++j) {
            for (uint i = 0; i < velocityY.getSize().x; ++i) {
                const Index2 id(i, j);
                if (velocityYWeight[id] > eps)
                    velocityY[id] /= velocityYWeight[id];
            }
        }

        // Current grid velocities
        oldVelocityX = velocityX;
        oldVelocityY = velocityY;

    } else {
        // Ink semi-Lagrangian advection -->pr 1
        {
            const Index2 size = grid.getSize();
            const Array2<Vector3> oldInk = inkRGB;
            const Array2<float> oldVelocityX = velocityX;
            const Array2<float> oldVelocityY = velocityY;

            for (uint j = 0; j < size.y; ++j) {
                for (uint i = 0; i < size.x; ++i) {
                    const Vector2 pos = grid.getCellPos(Index2(i, j));
                    const Vector2 vel = getVelocity(grid, oldVelocityX, oldVelocityY, pos);
                    const Vector2 prevPos = pos - vel * dt;
                    inkRGB[Index2(i, j)] = sampleBilerpVector3(oldInk, grid.getCellIndex(prevPos));
                }
            }
        }

        // Velocity semi-Lagrangian advection
        {
            const Array2<float> oldVelocityX = velocityX;
            const Array2<float> oldVelocityY = velocityY;

            for (uint j = 0; j < velocityX.getSize().y; ++j) {
                for (uint i = 0; i < velocityX.getSize().x; ++i) {
                    const Vector2 pos = grid.getFacePosX(Index2(i, j));
                    const Vector2 vel = getVelocity(grid, oldVelocityX, oldVelocityY, pos);
                    const Vector2 prevPos = pos - vel * dt;
                    velocityX[Index2(i, j)] = sampleBilerpFloat(oldVelocityX, grid.getFaceIndexX(prevPos));
                }
            }

            for (uint j = 0; j < velocityY.getSize().y; ++j) {
                for (uint i = 0; i < velocityY.getSize().x; ++i) {
                    const Vector2 pos = grid.getFacePosY(Index2(i, j));
                    const Vector2 vel = getVelocity(grid, oldVelocityX, oldVelocityY, pos);
                    const Vector2 prevPos = pos - vel * dt;
                    velocityY[Index2(i, j)] = sampleBilerpFloat(oldVelocityY, grid.getFaceIndexY(prevPos));
                }
            }
        }
    }
}

// Emitters --> first practice 
void Fluid2::fluidEmission()
{
    struct Emitter {
        Vector2 min, max;
        Vector3 color;
        float vel;
    };

    Emitter emetters[] = {{Vector2(-0.1f, -1.9f), Vector2(0.1f, -1.75f), Vector3(1.0f, 1.0f, 0.0f), 8.0f},
                          {Vector2(-0.2f, -1.9f), Vector2(-0.1f, -1.75f), Vector3(1.0f, 0.0f, 1.0f), 8.0f},
                          {Vector2(0.1f, -1.9f), Vector2(0.2f, -1.75f), Vector3(0.0f, 1.0f, 1.0f), 8.0f}};

   if (flipEnabled) {
        const Vector2 minDomain = grid.getDomain().minPosition;
        const Vector2 dx = grid.getDx();
        const float eps = 1e-6f;
        const int offset = 3;

        for (uint p = 0; p < particles.getSize(); ++p) {
            const Vector2 pos = particles.getPosition(p);

            for (uint e = 0; e < 3; ++e) {
                const int iStart = (int)std::floor((emetters[e].min.x - minDomain.x + eps) / dx.x);
                const int iEnd = (int)std::floor((emetters[e].max.x - minDomain.x - eps) / dx.x);

                const int jStart = (int)std::floor((emetters[e].min.y - minDomain.y + eps) / dx.y) + offset;
                const int jEnd = (int)std::floor((emetters[e].max.y - minDomain.y - eps) / dx.y) + offset;

                const Vector2 cellIndex = grid.getCellIndex(pos);
                const int i = (int)std::floor(cellIndex.x + 0.5f);
                const int j = (int)std::floor(cellIndex.y + 0.5f);

                if (i >= iStart && i <= iEnd && j >= jStart && j <= jEnd) {
                    particles.setInk(p, emetters[e].color);
                    particles.setVelocity(p, Vector2(0.0f, emetters[e].vel));
                }
            }
        }

   } else {
        const Vector2 minDomain = grid.getDomain().minPosition;
        const Vector2 dx = grid.getDx();
        const float eps = 1e-6f;

        for (uint e = 0; e < 3; ++e) {
            const int iStart = (int)std::floor((emetters[e].min.x - minDomain.x + eps) / dx.x);
            const int iEnd = (int)std::floor((emetters[e].max.x - minDomain.x - eps) / dx.x);

            const int jStart = (int)std::floor((emetters[e].min.y - minDomain.y + eps) / dx.y) + 3;
            const int jEnd = (int)std::floor((emetters[e].max.y - minDomain.y - eps) / dx.y) + 3;

            for (int j = jStart; j <= jEnd; ++j) {
                for (int i = iStart; i <= iEnd; ++i) {
                    if (i >= 0 && i < (int)grid.getSize().x && j >= 0 && j < (int)grid.getSize().y) {
                        inkRGB[Index2((uint)i, (uint)j)] = emetters[e].color;
                        velocityY[Index2((uint)i, (uint)j + 1)] = emetters[e].vel;
                    }
                }
            }
        }
   }
}

void Fluid2::fluidVolumeForces(const float dt)
{
    const float g = Scene::kGravity;

    for (uint j = 1; j < velocityY.getSize().y - 1; ++j) {
        for (uint i = 0; i < velocityY.getSize().x; ++i) {
            const Vector3 color1 = inkRGB[Index2(i, j - 1)];
            const Vector3 color2 = inkRGB[Index2(i, j)];

            const float inkDown = std::max(color1.x, std::max(color1.y, color1.z));
            const float inkUp = std::max(color2.x, std::max(color2.y, color2.z));
            const float inkMean = 0.5f * (inkDown + inkUp);

            velocityY[Index2(i, j)] += dt * (-g) * inkMean;
        }
    }
}

void Fluid2::fluidViscosity(const float dt)
{
    const float mu = Scene::kViscosity;
    const float rho = Scene::kDensity;

    if (mu <= 0.0f)
        return;

    const Vector2 dx = grid.getDx();
    const float dx2 = dx.x * dx.x;
    const float dy2 = dx.y * dx.y;

    const Array2<float> oldVelocityX = velocityX;
    const Array2<float> oldVelocityY = velocityY;

    for (uint j = 0; j < velocityX.getSize().y; ++j) {
        for (uint i = 0; i < velocityX.getSize().x; ++i) {
            const uint iM = (i > 0) ? i - 1 : i;
            const uint iP = (i < velocityX.getSize().x - 1) ? i + 1 : i;
            const uint jM = (j > 0) ? j - 1 : j;
            const uint jP = (j < velocityX.getSize().y - 1) ? j + 1 : j;

            const float laplacian =
                (oldVelocityX[Index2(iP, j)] + oldVelocityX[Index2(iM, j)] - 2.0f * oldVelocityX[Index2(i, j)]) / dx2 +
                (oldVelocityX[Index2(i, jP)] + oldVelocityX[Index2(i, jM)] - 2.0f * oldVelocityX[Index2(i, j)]) / dy2;

            velocityX[Index2(i, j)] += dt * mu / rho * laplacian;
        }
    }

    for (uint j = 0; j < velocityY.getSize().y; ++j) {
        for (uint i = 0; i < velocityY.getSize().x; ++i) {
            const uint iM = (i > 0) ? i - 1 : i;
            const uint iP = (i < velocityY.getSize().x - 1) ? i + 1 : i;
            const uint jM = (j > 0) ? j - 1 : j;
            const uint jP = (j < velocityY.getSize().y - 1) ? j + 1 : j;

            const float laplacian =
                (oldVelocityY[Index2(iP, j)] + oldVelocityY[Index2(iM, j)] - 2.0f * oldVelocityY[Index2(i, j)]) / dx2 +
                (oldVelocityY[Index2(i, jP)] + oldVelocityY[Index2(i, jM)] - 2.0f * oldVelocityY[Index2(i, j)]) / dy2;

            velocityY[Index2(i, j)] += dt * mu / rho * laplacian;
        }
    }
}

void Fluid2::fluidPressureProjection(const float dt)
{
    const Index2 size = grid.getSize();
    const uint Nx = size.x;
    const uint Ny = size.y;
    const float dx = grid.getDx().x;
    const float rho = Scene::kDensity;

    // Solid boundaries --> normal velocity equal to zero
    for (uint j = 0; j < Ny; ++j) {
        velocityX[Index2(0, j)] = 0.0f;
        velocityX[Index2(Nx, j)] = 0.0f;
    }
    for (uint i = 0; i < Nx; ++i) {
        velocityY[Index2(i, 0)] = 0.0f;
        velocityY[Index2(i, Ny)] = 0.0f;
    }

    //Right hand side, divergence of the velocity field
    std::vector<float> rhs(Nx * Ny, 0.0f);

    for (uint j = 0; j < Ny; ++j) {
        for (uint i = 0; i < Nx; ++i) {
            const float div = (velocityX[Index2(i + 1, j)] - velocityX[Index2(i, j)]) +
                              (velocityY[Index2(i, j + 1)] - velocityY[Index2(i, j)]);

            rhs[i + j * Nx] = -div * rho * dx / dt;
        }
    }

    // Matrix for pressure.
    SparseMatrixf A(Nx * Ny, 5);
    for (uint j = 0; j < Ny; ++j) {
        for (uint i = 0; i < Nx; ++i) {
            const uint row = i + j * Nx;
            float diagonal = 4.0f;

            if (i > 0)
                A.set_element(row, (i - 1) + j * Nx, -1.0f);
            else
                diagonal -= 1.0f;

            if (i < Nx - 1)
                A.set_element(row, (i + 1) + j * Nx, -1.0f);
            else
                diagonal -= 1.0f;

            if (j > 0)
                A.set_element(row, i + (j - 1) * Nx, -1.0f);
            else
                diagonal -= 1.0f;

            if (j < Ny - 1)
                A.set_element(row, i + (j + 1) * Nx, -1.0f);
            else
                diagonal -= 1.0f;

            A.set_element(row, row, diagonal);
        }
    }

    // Solve pressure with given solver
    std::vector<float> P(Nx * Ny, 0.0f);
    PCGSolver<float> solver;
    solver.set_solver_parameters(1e-6f, 200);

    float residual;
    int iterations;
    solver.solve(A, rhs, P, residual, iterations);

    for (uint j = 0; j < Ny; ++j) {
        for (uint i = 0; i < Nx; ++i) {
            pressure[Index2(i, j)] = P[i + j * Nx];
        }
    }

    // Apply pressure gradient to velocities
    for (uint j = 0; j < Ny; ++j) {
        for (uint i = 1; i < Nx; ++i) {
            velocityX[Index2(i, j)] -= dt / (rho * dx) * (P[i + j * Nx] - P[(i - 1) + j * Nx]);
        }
    }

    for (uint j = 1; j < Ny; ++j) {
        for (uint i = 0; i < Nx; ++i) {
            velocityY[Index2(i, j)] -= dt / (rho * dx) * (P[i + j * Nx] - P[i + (j - 1) * Nx]);
        }
    }

    if (flipEnabled) {
        // Difference between the new grid velocities and the velocities saved before projection
        Array2<float> deltaVelocityX;
        Array2<float> deltaVelocityY;
        deltaVelocityX.resize(velocityX.getSize());
        deltaVelocityY.resize(velocityY.getSize());

        for (uint j = 0; j < velocityX.getSize().y; ++j) {
            for (uint i = 0; i < velocityX.getSize().x; ++i) {
                const Index2 id(i, j);
                deltaVelocityX[id] = velocityX[id] - oldVelocityX[id];
            }
        }

        for (uint j = 0; j < velocityY.getSize().y; ++j) {
            for (uint i = 0; i < velocityY.getSize().x; ++i) {
                const Index2 id(i, j);
                deltaVelocityY[id] = velocityY[id] - oldVelocityY[id];
            }
        }

        // PIC/FLIP blend --> mostly FLIP, with a small PIC contribution for stability
        for (uint p = 0; p < particles.getSize(); ++p) {
            const Vector2 pos = particles.getPosition(p);
            const Vector2 oldParticleVelocity = particles.getVelocity(p);

            const Vector2 picVelocity = getVelocity(grid, velocityX, velocityY, pos);
            const Vector2 flipDelta(sampleBilerpFloat(deltaVelocityX, grid.getFaceIndexX(pos)),
                                    sampleBilerpFloat(deltaVelocityY, grid.getFaceIndexY(pos)));
            const Vector2 flipVelocity = oldParticleVelocity + flipDelta;

            const Vector2 newParticleVelocity = flipVelocity * 0.95f + picVelocity * 0.05f;
            particles.setVelocity(p, newParticleVelocity);
        }
    }
}
}  
