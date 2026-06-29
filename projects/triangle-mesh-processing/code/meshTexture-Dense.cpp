/*
 * meshTexture-Dense.cpp
 *
 * Written by Jose Miguel Espadero <josemiguel.espadero@urjc.es>
 *
 * This code is written as material for the FMF class of the
 * Master Universitario en Informatica Grafica, Juegos y Realidad Virtual.
 * Its purpose is to be didactic and easy to understand, not hard optimized.
 * 
 * //TODO: Fill-in your name and email
 * Name of alumn: Filippo Falzi
 * Email of alumn: f.falzi.2025@alumnos.urjc.es
 * Year: 2025
 * 
 */

//This file is the simplest solution to the meshTexture exercise.
//Use a dense matrix and does not reduce the problem, so take long time to execute
//for files as mannequin2.ply

#define _CRT_NONSTDC_NO_DEPRECATE
#include <iostream>
#include <iomanip>
#include <unordered_set>
#include <queue>
#include <chrono>
using namespace std::chrono;

#include <SimpleMesh.hpp>
#include <TextureMesh.hpp>

//Check if Eigen  (a standar Matrix Library) is included. If not,
//you can get it at http://eigen.tuxfamily.org

// <Eigen/Dense> is the module for dense (traditional) matrix and vector.
// You can get a quick reference for using Eigen dense objects at:
// http://eigen.tuxfamily.org/dox/group__QuickRefPage.html
#include <Eigen/Core>
#include <Eigen/LU>

struct Angles
{
    double a1, a2, a3;
};

//Function to compute the angles of a triangle in a mesh
Angles computeAngle(const SimpleMesh& mesh, const SimpleTriangle& t) {
    vec3 A = mesh.coordinates[t.a];
    vec3 B = mesh.coordinates[t.b];
    vec3 C = mesh.coordinates[t.c];
    vec3 ab = B - A;
    vec3 ac = C - A;
    //Compute |ab|·|ac|
    double m = sqrt((ab * ab) * (ac * ac));
    double cosine = (ab * ac) / m;
    double sine = ab.cross(ac).module() / m;
    double angle1 = atan2(sine, cosine);

    vec3 ba = A - B;
    vec3 bc = C - B;
    //Compute |bc|·|ba|
    m = sqrt((ba * ba) * (bc * bc));
    cosine = (ba * bc) / m;
    sine = ba.cross(bc).module() / m;
    double angle2 = atan2(sine, cosine);

    vec3 ca = A - C;
    vec3 cb = B - C;
    m = sqrt((ca * ca) * (cb * cb));
    cosine = (ca * cb) / m;
    sine = ca.cross(cb).module() / m;
    double angle3 = atan2(sine, cosine);

    return { angle1, angle2, angle3 };
};

/// Update the contents of externalEdges and internalEdges 

// hash functions for internal / external edges check
struct SimpleEdgeHash {
    std::size_t operator()(const SimpleEdge& e) const noexcept {
        int minV = std::min(e.a, e.b);
        int maxV = std::max(e.a, e.b);

        return std::hash<int>()(minV) ^ (std::hash<int>()(maxV) << 1);
    }
};

void updateEdgeLists(const SimpleMesh &mesh, 
    std::unordered_set <SimpleEdge, SimpleEdgeHash>& externalEdges,
    std::unordered_set <SimpleEdge, SimpleEdgeHash>& internalEdges) {
   
    //TODO 2.1: Copy the body of the updateEdgeLists() method from meshBoundary
    for (const SimpleTriangle& t : mesh.triangles)
    {
        std::vector<SimpleEdge> triEdges(3);
        triEdges = t.edges();

        for (const SimpleEdge& e : triEdges) {
            //if (externalEdges.find(e) != externalEdges.end() || internalEdges.find(e) != internalEdges.end())
            //{
            //    std::cout << "The edge already exists, the mesh is not mainfold\n" << std::endl;
            //}

            if (externalEdges.find(e.reversed()) != externalEdges.end())
            {
                externalEdges.erase(e.reversed());
                internalEdges.insert(e);
            }
            else {

                externalEdges.insert(e);
            }
        }

    }
    //if (externalEdges.empty()) {
    //    std::cout << "The mesh has no boundary\n" << std::endl;
    //}
    //END TODO 2.1

}//void updateEdgeLists()

/// Write an Eigen Matrix to a matlab file
void exportDenseToMatlab (const Eigen::MatrixXd &m, const std::string &filename, const std::string matrixName ="A")
{
    std::cout << "Export matrix "<< matrixName << " to file: " << filename << std::endl;

    //Open the file as a stream
    ofstream os(filename.c_str());
    if (!os.is_open())
        throw filename + string(": Error creating the file");


    os << "# name: "<<matrixName << std::endl
       << "# type: matrix" << std::endl
       << "# rows: " << m.rows() << std::endl
       << "# columns: " << m.cols() << std::endl;
    Eigen::IOFormat fmt(Eigen::StreamPrecision, Eigen::DontAlignCols, " ", "\n", "", "", "", "\n");
    os << m.format(fmt);

    os.close();
    std::cout << "To import "<< matrixName <<" into matlab use the command: load(\""<<filename<<"\")"<<std::endl;

}//void exportDenseToMatlab (const &MatrixXd m, const std::string &filename)


int main(int argc, char* argv[])
{
    //Set dumpMatrix to true for debugging. Writting matrix to file can take some time
    bool dumpMatrix = false;

    try
    {
        // Set default input mesh filename
        std::string filename("mallas/16Triangles.off");  //Minimal case test
        //std::string filename("mallas/mask2.ply");      //Easy case test
        //std::string filename("mallas/mannequin2.ply"); //Medium case test
        //std::string filename("mallas/laurana50k.ply"); //Really hard for dense matrix
        if (argc > 1)
            filename = std::string(argv[1]);

        ///////////////////////////////////////////////////////////////////////
        //Step 1.
        //Read an input mesh
        SimpleMesh mesh;
        std::cout << "Loading file " << filename << endl;
        mesh.readFile(filename, false);

        std::cout << "Num vertex: " << mesh.numVertex() << " Num triangles: " << mesh.numTriangles()
            << " Unreferenced vertex: " << mesh.checkUnreferencedVertex() << endl;

        //Set dumpMatrix to true for debugging. Writting matrix to file can take some time
        dumpMatrix = dumpMatrix || (mesh.numVertex() < 40);

        //Time measure
        high_resolution_clock::time_point clock0 = high_resolution_clock::now();

        ///////////////////////////////////////////////////////////////////////
        //Step 2.
        //Compute edge list and show it. This step should work if you correctly
        //finished the meshBoundary exercise.
        std::unordered_set <SimpleEdge, SimpleEdgeHash> externalEdges;
        std::unordered_set <SimpleEdge, SimpleEdgeHash> internalEdges;
        updateEdgeLists(mesh, externalEdges, internalEdges);

        //Count num of internal and external vertex
        size_t numVertex = mesh.coordinates.size();
        size_t numOfExternalVertex = externalEdges.size();
        //size_t numOfInternalVertex = numVertex - numOfExternalVertex;
        

        //Dump external edges
        std::cout << numOfExternalVertex << " vertex in external boundary: " << endl;
        if (numOfExternalVertex < 80)
        {
            for (auto& e : externalEdges)
            {
                std::cout << "[" << e.a << "->" << e.b << "] ";
            }
            std::cout << endl;
        }
        
        //Build the list of external vertex
        std::vector<size_t>externalVertex;
        for (auto& e : externalEdges)
        {
            externalVertex.push_back(e.a);
        }

        //Optimization: Keep a lookup table to ask if one vertex is external
        std::vector<bool>isExternal(numVertex, false);
        for (size_t i = 0; i < numOfExternalVertex; i++)
        {
            isExternal[externalVertex[i]] = true;
        }

        std::cout << "Done compute Boundary. " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;

        ///////////////////////////////////////////////////////////////////////
        //Step 3.
        //Build Laplace matrix (step 1)
        std::cout << "Computing Laplacian matrix (dense):" << endl;

        //We will use a dense matrix. If you want to use sparse matrix
        //you have to check http://eigen.tuxfamily.org/dox/group__TutorialSparse.html
        //and declare a sparse matrix instead.
        Eigen::MatrixXd meshMatrix(numVertex, numVertex);

        //TODO 3.1: Build the Laplacian matrix using the weights
        //
        //      /\         .  Lij = cot(α) + cot(β) , if vertex i neighbour of j
        //     /β \        .
        //    /    \       .  Lii = -Sum Lij , diagonal element is the sum of row
        //   /      \      .
        // vi--------vj    .
        //   \      /      .
        //    \    /       .
        //     \α /        .
        //      \/         .
        //

        meshMatrix.setZero();
        for (const SimpleTriangle t : mesh.triangles) {
            Angles tAngles = computeAngle(mesh, t);
            double Lij1 = 1 / tan(tAngles.a1);
            double Lij2 = 1 / tan(tAngles.a2);
            double Lij3 = 1 / tan(tAngles.a3);

            meshMatrix(t.a, t.b) += Lij3;
            meshMatrix(t.a, t.c) += Lij2;
            meshMatrix(t.a, t.a) -= Lij3 + Lij2;

            meshMatrix(t.b, t.c) += Lij1;
            meshMatrix(t.b, t.a) += Lij3;
            meshMatrix(t.b, t.b) -= Lij1 + Lij3;

            meshMatrix(t.c, t.a) += Lij2;
            meshMatrix(t.c, t.b) += Lij1;
            meshMatrix(t.c, t.c) -= Lij2 + Lij1;
        }
        //END TODO 3.1

        std::cout << "Done Laplacian matrix (dense). " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;

        if (dumpMatrix)
            exportDenseToMatlab(meshMatrix, "laplacian.mat", "L");

        ///////////////////////////////////////////////////////////////////////
        //Step 5.1
        //Build system matrix
        std::cout << "Computing System matrix:" << endl;

        //TODO 3.2: Patch Laplace matrix to generate a valid system of equations
        for (size_t i = 0; i < mesh.coordinates.size(); i++) {
            if (isExternal[i]){             
                meshMatrix.row(i).setZero();
                meshMatrix(i, i) = 1;
            }
        }
        //END TODO 3.2

        std::cout << "Done System matrix. " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;

        if (dumpMatrix)
            exportDenseToMatlab(meshMatrix, "systemMatrix.mat", "A");

        ///////////////////////////////////////////////////////////////////////
        //Step 5.2
        //Build UV values for external vertex, using the boundary of a square.
        std::cout << "Computing contour conditions:" << endl;

        //Note that this is a matrix of numvertex rows and 2 columns 

        //TODO 3.3: Build a set of valid UV values for external vertex, mapping
        //the vertex to the boundary of a square of side unit.
       
        //Ordering the boundary
        float xMin = INFINITY;
        float yMin = INFINITY;
        size_t idxMin = 0;

        // Starting the path form the vertex in the left bottom part of the mesh
        for (size_t i = 0; i < externalVertex.size(); i++) {
            if (mesh.coordinates[externalVertex[i]].X < xMin ||
                (mesh.coordinates[externalVertex[i]].X == xMin && mesh.coordinates[externalVertex[i]].Y < yMin)) {
                yMin = mesh.coordinates[externalVertex[i]].Y;
                xMin = mesh.coordinates[externalVertex[i]].X;
                idxMin = externalVertex[i];
            }
        }

        size_t startVertex = idxMin;
        std::vector<size_t> orderedExternalVertex;
        orderedExternalVertex.push_back(startVertex);
        std::unordered_map <size_t, size_t > adjacent;
       
        // Assuming anti clock_wise edges
        for (SimpleEdge e : externalEdges) {
            adjacent[e.a] = e.b;
        }
        while (orderedExternalVertex.size() < externalVertex.size())
        {
            size_t lastVertex = orderedExternalVertex.back();
            size_t nextVertex = adjacent[lastVertex];
            orderedExternalVertex.push_back(nextVertex);
        } // end creation ordered boundary array with indices

        Eigen::MatrixX2d UV_0(numVertex, 2);
        UV_0.setZero();

        // array of vec2 containing the coordinates of the perimeter suddivision
        std::vector<Eigen::RowVector2d> contour;

        // choosen contour: square with 1 unit sides
        double squareSideLength = 1.0;

        size_t perSide = (externalVertex.size() - 4) / 4 + 1;
        size_t resto = externalVertex.size() % 4;
        std::vector<size_t> vertexPerSide(4, perSide); // Vertex in each side of the sqare
        size_t count = 0;
        size_t side = 0;

        // Distributing remained vertices on the sides of the square
        if (resto != 0) {
            for (size_t i = 0; i < resto; i++) // Maximum remained vertices = 3, Numb. of sides = 4
                vertexPerSide[i]++;
        }

        // Creation of suddivision
        for (size_t i = 0; i < externalVertex.size(); i++) {

            double t = (vertexPerSide[side] > 1) ? double(count) * squareSideLength / double(vertexPerSide[side]) : 0.0;

            switch (side) {
            case 0:
                contour.push_back(Eigen::Vector2d(t, 0));
                break;
            case 1:
                contour.push_back(Eigen::Vector2d(1, t));
                break;
            case 2:
                contour.push_back(Eigen::Vector2d(1 - t, 1));
                break;
            case 3:
                contour.push_back(Eigen::Vector2d(0, 1 - t));
                break;
            }
            count++;

            if (count >= vertexPerSide[side]) {
                side++;
                count = 0;
            }
        }

        size_t idx = 0;
        //for (const auto& v : contour) {
        //    std::cout << "contour[" << idx++ << "] = (" << v(0) << ", " << v(1) << ")" << std::endl;
        //}

        // Finally, assign UV_0 values
        for (size_t i = 0; i < contour.size(); i++) {
            UV_0.row(orderedExternalVertex[i]) = contour[i];
        }

        //END TODO 3.3

        std::cout << "Done contour conditions. " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;

        if (dumpMatrix)
            exportDenseToMatlab(UV_0, "boundaryUVO.mat", "UV0");

        ///////////////////////////////////////////////////////////////////////
        //Step 6

        //We will use Eigen to solve the matrix from here. 
        std::cout << "Solving the system using Eigen (dense):" << endl;

        //Solve Ax = b; where A = meshMatrix and b = UV_0
        //This code is valid only for solving a dense matrix. If you want to use sparse matrix
        //you have to check http://eigen.tuxfamily.org/dox/group__TopicSparseSystems.html
        Eigen::PartialPivLU<Eigen::MatrixXd>meshMatrixLU = meshMatrix.lu();
        Eigen::MatrixX2d UV = meshMatrixLU.solve(UV_0);


        std::cout << "Done solving the system. " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;

        //Dump the computed solution to the system
        if (dumpMatrix)
            exportDenseToMatlab(UV, "solutionUV.mat", "UV");

        ///////////////////////////////////////////////////////////////////////
        //Step 6.4 

        //Create a planar mesh (reverse UV mesh)
        SimpleMesh planarMesh;
        //Use UV values as geometrical coordinates and same triangles that input mesh
        planarMesh.coordinates.resize(numVertex);
        for (size_t i = 0; i < numVertex; i++)
        {
            planarMesh.coordinates[i].set(UV(i, 0), UV(i, 1), 0);
        }
        planarMesh.triangles = mesh.triangles;

        string output_UVMesh1 = "output_UVMesh1.ply";
        std::cout << "Saving parameterization mesh to " << output_UVMesh1 << endl;
        //planarMesh.writeFileOBJ(output_UVMesh1);
        planarMesh.writeFilePLY(output_UVMesh1);


        //Create a TextureMesh mesh with UV coordinates
        TextureMesh textureMesh;
        textureMesh.coordinates = mesh.coordinates;
        textureMesh.triangles = mesh.triangles;

        //Set image filename to be used as texture
        textureMesh.textureFile = "UVchecker.jpg";

        //Set UV as texture-per-vertex coordinates
        textureMesh.UV.resize(numVertex);
        for (size_t i = 0; i < numVertex; i++)
            textureMesh.UV[i].set(float(UV(i, 0)), float(UV(i, 1)));

        //Dump textureMesh to file (.obj or .ply)
        string output_UVMesh2 = "output_UVMesh2.ply";
        std::cout << "Saving texture mesh to " << output_UVMesh2 << endl;
        //textureMesh.writeFileOBJ(output_UVMesh2);
        textureMesh.writeFilePLY(output_UVMesh2);

        //Visualize the file with an external viewer
#ifdef WIN32
        //string viewcmd = "\"C:\\Program Files (x86)\\VCG\\MeshLab\\meshlab.exe\"";
        string viewcmd = "\"C:/Programmi/VCG/MeshLab/meshlab.exe\"";
#else
        string viewcmd = "meshlab >/dev/null 2>&1 ";
#endif
        string cmd = viewcmd + " " + output_UVMesh2;
        std::cout << "Executing external command: " << cmd << endl;
        return system(cmd.c_str());

    }//try
    catch (const string& str) { std::cerr << "EXCEPTION: " << str << std::endl; }
    catch (const char* str) { std::cerr << "EXCEPTION: " << str << std::endl; }
    catch (std::exception& e) { std::cerr << "EXCEPTION: " << e.what() << std::endl; }
    catch (...) { std::cerr << "EXCEPTION (unknow)" << std::endl; }

#ifdef WIN32
    std::cout << "Press Return to end the program" << endl;
    std::cin.get();
#else
#endif

    return 0;
}

