#include "Scene.h"

#include "Numeric/PCGSolver.h"

namespace asa
{
namespace
{
////////////////////////////////////////////////
// Interpolation function for stepback resolution in the grid

// Interpolation for ink RGB values
asa::Vector3 sampleBilerpVector3(const asa::Array2<asa::Vector3> &array, const asa::Vector2 &uv)
{
    asa::Index2 size = array.getSize();
    // Getting minimum bottom and left index
    int i = (int)std::floor(uv.x);
    int j = (int)std::floor(uv.y);
    // Weights
    float tx = uv.x - (float)i;
    float ty = uv.y - (float)j;

    // Getting the four faces to interpolate
    uint i0 = (uint)std::max(0, std::min(i, (int)size.x - 1));
    uint i1 = (uint)std::max(0, std::min(i + 1, (int)size.x - 1));
    uint j0 = (uint)std::max(0, std::min(j, (int)size.y - 1));
    uint j1 = (uint)std::max(0, std::min(j + 1, (int)size.y - 1));

    return asa::bilerp(
        array[Index2(i0, j0)], array[Index2(i1, j0)], array[Index2(i0, j1)], array[Index2(i1, j1)], tx, ty);
}

// For velocity interpolation -> U & V
// Same logic of the previous function, uses templates to give back one float
float sampleBilerpFloat(const asa::Array2<float> &array, const asa::Vector2 &uv)
{
    asa::Index2 size = array.getSize();
    int i = (int)std::floor(uv.x);
    int j = (int)std::floor(uv.y);
    float tx = uv.x - (float)i;
    float ty = uv.y - (float)j;

    uint i0 = (uint)std::max(0, std::min(i, (int)size.x - 1));
    uint i1 = (uint)std::max(0, std::min(i + 1, (int)size.x - 1));
    uint j0 = (uint)std::max(0, std::min(j, (int)size.y - 1));
    uint j1 = (uint)std::max(0, std::min(j + 1, (int)size.y - 1));

 return asa::bilerp(
        array[Index2(i0, j0)], array[Index2(i1, j0)], 
        array[Index2(i0, j1)], array[Index2(i1, j1)], 
        tx, ty);
}

// Function to compose velocity from grid
Vector2 getVelocity(const asa::Grid2 &grid,
                    const asa::Array2<float> &velocityX,
                    const asa::Array2<float> &velocityY,
                    const Vector2 &pos)
{
    // These two functions adjust the pos considering 
    // the velocities positions on the grid (-0.5) 
    Vector2 uvU = grid.getFaceIndexX(pos);
    Vector2 uvV = grid.getFaceIndexY(pos);

    // Using the funcion to interpolate U & V
    float u = sampleBilerpFloat(velocityX, uvU);
    float v = sampleBilerpFloat(velocityY, uvV);

    // Final v with a weighted value depending on its position in the grid
    return Vector2(u, v);
}
////////////////////////////////////////////////
}  // namespace

// Advection
// FIRST let's apply semi_lagrangian method for the color
void asa::Fluid2::fluidAdvection(const float dt)
{
    // Size is the grid size
    Index2 size = grid.getSize();
    auto inkCopy = this->inkRGB;

    // Copying velocity values before applying any change
    auto uPrev = this->velocityX;
    auto vPrev = this->velocityY;

    for (uint j = 0; j < size.y; ++j) {
        for (uint i = 0; i < size.x; ++i) {
            Vector2 x = grid.getCellPos(Index2(i, j));

            // The grid extremes exceed the size number of one unit
            float u = (uPrev[Index2(i, j)] + uPrev[Index2(i + 1, j)]) * 0.5f;
            float v = (vPrev[Index2(i, j)] + vPrev[Index2(i, j + 1)]) * 0.5f;
            // vlocity vector at t in the center of the cell
            Vector2 vel(u, v);

            // Gouing backwards --> -dt
            Vector2 xPrev = x - vel * dt;

            Vector2 ijPrev = grid.getCellIndex(xPrev);

            this->inkRGB[Index2(i, j)] = sampleBilerpVector3(inkCopy, ijPrev);
        }
    }

    // SEECOND, let's update the velocity values
    // Advection Velovity X
    Index2 sizeU = velocityX.getSize();
    for (uint j = 0; j < sizeU.y; ++j) {
        for (uint i = 0; i < sizeU.x; ++i) {
            Vector2 x = grid.getFacePosX(Index2(i, j));
            Vector2 vel = getVelocity(grid, uPrev, vPrev, x);
            Vector2 xPrev = x - vel * dt;
            this->velocityX[Index2(i, j)] = sampleBilerpFloat(uPrev, grid.getFaceIndexX(xPrev));
        }
    }

    // Advection Velocity Y
    Index2 sizeV = velocityY.getSize();
    for (uint j = 0; j < sizeV.y; ++j) {
        for (uint i = 0; i < sizeV.x; ++i) {
            Vector2 x = grid.getFacePosY(Index2(i, j));
            Vector2 vel = getVelocity(grid, uPrev, vPrev, x);
            Vector2 xPrev = x - vel * dt;
            this->velocityY[Index2(i, j)] = sampleBilerpFloat(vPrev, grid.getFaceIndexY(xPrev));
        }
    }
}

void Fluid2::fluidEmission()
{
    if (Scene::testcase >= Scene::SMOKE) {
        struct Emitter {
            Vector2 min, max;
            Vector3 color;
            float vel;
        };

        Emitter emetters[] = {
            {Vector2(-0.1f, -1.9f), Vector2(0.1f, -1.75f), Vector3(1.0, 1.0, 0.0), 8.0f},   // yellow
            {Vector2(-0.2f, -1.9f), Vector2(-0.1f, -1.75f), Vector3(1.0, 0.0, 1.0), 8.0f},  // magenta
            {Vector2(0.1f, -1.9f), Vector2(0.2f, -1.75f), Vector3(0.0, 1.0, 1.0), 8.0f}     // ciano
        };

        Index2 size = grid.getSize();
        Vector2 minDomain = grid.getDomain().minPosition;
        Vector2 dx = grid.getDx();

        // Adding a tollerance --> otherwise the emitters are  asymetric
        float eps = 1e-6f;

       for (const auto &em : emetters) {
            int iStart = (int)std::floor((em.min.x - minDomain.x + eps) / dx.x);
            int iEnd = (int)std::floor((em.max.x - minDomain.x - eps) / dx.x);

            int offset = 3; // To have a configuration similar to the one shown in class  
            int jStart = (int)std::floor((em.min.y - minDomain.y + eps) / dx.y) + offset;
            int jEnd = (int)std::floor((em.max.y - minDomain.y - eps) / dx.y) + offset;

            for (int j = jStart; j <= jEnd; ++j) {
                for (int i = iStart; i <= iEnd; ++i) {
                        
                        this->inkRGB[Index2(i, j)] = em.color;
                        this->velocityY[Index2(i, j + 1)] = em.vel;
                       
                        // Not necessary
                        //this->velocityX[Index2(i, j)] = 0.0f;
                        //this->velocityX[Index2(i + 1, j)] = 0.0f;
                    
                }
            }
        }
    }
}

void Fluid2::fluidVolumeForces(const float dt)
{
    if (Scene::testcase >= Scene::SMOKE) {
        const float g = Scene::kGravity;
        Index2 sizeV = velocityY.getSize();

        // We must consider that tha air is hevaier than the smoke --> Kinda Archimede's push
        // Otherwise the smoke in reality would fall downwards

        for (uint j = 1; j < sizeV.y - 1; ++j) {
            for (uint i = 0; i < sizeV.x; ++i) {
           
                // Getting color of two adjacent cells
                Vector3 color1 = this->inkRGB[Index2(i, j - 1)];
                Vector3 color2 = this->inkRGB[Index2(i, j)];

                // Using maximum values --> each color has the same density
                float inkDown = std::max(color1.x, std::max(color1.y, color1.z));
                float inkUp = std::max(color2.x, std::max(color2.y, color2.z));
                float inkMean = (inkDown + inkUp) * 0.5f;

                float archimede_and_gravity = -g * inkMean;

                this->velocityY[Index2(i, j)] += dt * archimede_and_gravity;
            }
        }
    }
}

void Fluid2::fluidViscosity(const float dt)
{
    if (Scene::testcase >= Scene::SMOKE) {
        const float mu = Scene::kViscosity;
        const float rho = Scene::kDensity;

        if (mu <= 0.0f)
            return;

        const float dx2 = grid.getDx().x * grid.getDx().x;
        const float dy2 = grid.getDx().y * grid.getDx().y;

        auto uPrev = this->velocityX;
        auto vPrev = this->velocityY;


        // VELOCITY X
        Index2 sizeU = velocityX.getSize();
        // Foreach face i, j
        for (uint j = 0; j < sizeU.y; ++j) {
            for (uint i = 0; i < sizeU.x; ++i) {

                uint iM = (i > 0) ? i - 1 : i;
                uint iP = (i < sizeU.x - 1) ? i + 1 : i;
                uint jM = (j > 0) ? j - 1 : j;
                uint jP = (j < sizeU.y - 1) ? j + 1 : j;

                float laplacianU = (uPrev[Index2(iP, j)] + uPrev[Index2(iM, j)] - 2.0f * uPrev[Index2(i, j)]) / dx2 +
                                   (uPrev[Index2(i, jP)] + uPrev[Index2(i, jM)] - 2.0f * uPrev[Index2(i, j)]) / dy2;

                this->velocityX[Index2(i, j)] += dt * mu / rho * laplacianU;
            }
        }
        // VELOCITY Y
        Index2 sizeV = velocityY.getSize();
        // Foreach face i, j
        for (uint j = 0; j < sizeV.y; ++j) {
            for (uint i = 0; i < sizeV.x; ++i) {
                uint iM = (i > 0) ? i - 1 : i;
                uint iP = (i < sizeV.x - 1) ? i + 1 : i;
                uint jM = (j > 0) ? j - 1 : j;
                uint jP = (j < sizeV.y - 1) ? j + 1 : j;

                float laplacianV = (vPrev[Index2(iP, j)] + vPrev[Index2(iM, j)] - 2.0f * vPrev[Index2(i, j)]) / dx2 +
                                   (vPrev[Index2(i, jP)] + vPrev[Index2(i, jM)] - 2.0f * vPrev[Index2(i, j)]) / dy2;

                this->velocityY[Index2(i, j)] += dt * mu / rho * laplacianV;
            }
        }
    }
}

 void Fluid2::fluidPressureProjection(const float dt)
{
 if (Scene::testcase >= Scene::SMOKE) {
        const Index2 size = grid.getSize();
        const uint Nx = size.x;
        const uint Ny = size.y;
        const float dx = grid.getDx().x;
        const float rho = Scene::kDensity;

        // Set normal velocity components in all boundaries to 0 --> DIRICHLET
        for (uint j = 0; j < Nx; ++j) {
            velocityX[Index2(0, j)] = 0.0f;
            velocityX[Index2(Nx, j)] = 0.0f;
        }
        for (uint i = 0; i < Ny; ++i) {
            velocityY[Index2(i, 0)] = 0.0f;
            velocityY[Index2(i, Ny)] = 0.0f;
        }

        // 2. Fill RHS (b) 
        // b = - (div U) * (rho * dx^2 / dt)
        std::vector<float> rhs(Nx * Ny, 0.0f);

        for (uint j = 0; j < Ny; ++j) {
            for (uint i = 0; i < Nx; ++i) {
                float div = (velocityX[Index2(i + 1, j)] - velocityX[Index2(i, j)]) +
                            (velocityY[Index2(i, j + 1)] - velocityY[Index2(i, j)]);

                rhs[i + j * Nx] = -div * rho * dx / dt;
            }
        }

        // 3. Fill A (Matrice di Poisson)
        SparseMatrixf A(Nx * Ny, 5);
        for (uint j = 0; j < Ny; ++j) {
            for (uint i = 0; i < Nx; ++i) {
                unsigned int row = i + j * Nx;

                float diagonal = 4.0f;

                if (i > 0)
                    A.set_element(row, (i - 1) + j * Nx, -1.0f);
                else
                    diagonal -= 1.0f; // Neumann --> dP/dn = 0

                if (i < Nx - 1)
                    A.set_element(row, (i + 1) + j * Nx, -1.0f);
                else
                    diagonal -= 1.0f; // Neumann

                if (j > 0)
                    A.set_element(row, i + (j - 1) * Nx, -1.0f);
                else
                    diagonal -= 1.0f; // Neumann

                if (j < Ny - 1)
                    A.set_element(row, i + (j + 1) * Nx, -1.0f);
                else
                    diagonal -= 1.0f; // Neumann

                A.set_element(row, row, diagonal);
                // It's not necessary to divide for dX^2 --> moved to the other side 
                // The matrix works just because dx = dy --> must be changed in chase the grid is not squared
            }
        }

        // 4. Solve Ax = b
        std::vector<float> P(Nx * Ny, 0.0f);  
        PCGSolver<float> solver;
        solver.set_solver_parameters(1e-6f, 200);  

        float residual;
        int iterations;
        solver.solve(A, rhs, P, residual, iterations);

        // 5. Apply P to the grid and Update Velocities
        // u_new = u_old - (dt/rho) * (grad P)
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
    }
}
}  // namespace asa
