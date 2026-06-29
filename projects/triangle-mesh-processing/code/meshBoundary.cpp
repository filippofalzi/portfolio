/*
 * meshBoundary.cpp
 *
 * Written by Jose Miguel Espadero <josemiguel.espadero@urjc.es>
 *
 * This code is written as material for the FMF class of the
 * Master Universitario en Informatica Grafica, Juegos y Realidad Virtual.
 * Its purpose is to be didactic and easy to understand, not hard optimized.
 *
 * This file computes the list of internal and external edges of a mesh
 * and writes the boundary (list of external edges). Then create a copy
 * of the mesh over a colorMesh and assign the red color to the vertex
 * in the boundary.
 * 
 * //TODO: Fill-in your name and email
 * Name of alumn: Filippo Falzi
 * Email of alumn: f.falzi.2025@alumnos.urjc.es
 * Year: 2025
 * 
 */

#ifdef _MSC_VER
#pragma warning(error: 4101)
#endif

#define _CRT_NONSTDC_NO_DEPRECATE
#include <iostream>
#include <cmath>
#include <SimpleMesh.hpp>
#include <ColorMesh.hpp>
#include <chrono>
#include <unordered_set>
#include <queue>
using namespace std::chrono;

// Hash functions for internal/external edges check
struct SimpleEdgeHash {
    std::size_t operator()(const SimpleEdge& e) const noexcept {
        int minV = std::min(e.a, e.b);
        int maxV = std::max(e.a, e.b);

        return std::hash<int>()(minV) ^ (std::hash<int>()(maxV) << 1);
    }
};

// Update the contents of externalEdges and internalEdges 
void updateEdgeLists(const SimpleMesh &mesh, 
                     std::unordered_set <SimpleEdge, SimpleEdgeHash> &externalEdges,
                     std::unordered_set <SimpleEdge, SimpleEdgeHash> & internalEdges)
    
    //TODO 2.1: Implement the body of the updateEdgeLists() method
{
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


// Pseudo Dijkstra algorithm
std::vector<float> Dijkstra(
    const unsigned& n,
    const std::vector<std::vector<int>>& neighbours,
    const std::vector<std::vector<float>>& edgeLengths,
    const std::vector<int>& sources)
{
    std::vector<float> paths(n, std::numeric_limits<float>::infinity());
    using Nodo = std::pair<float, int>;
    std::priority_queue<Nodo, std::vector<Nodo>, std::greater<Nodo>> pq;

    for (int s : sources) {
        paths[s] = 0.0f;
        pq.push({ 0.0f, s });
    }

    while (!pq.empty()) {
        int u = pq.top().second;
        float dist_u = pq.top().first;
        pq.pop();

        if (dist_u > paths[u]) continue;

        for (size_t i = 0; i < neighbours[u].size(); ++i) {
            int v = neighbours[u][i];
            float peso = edgeLengths[u][i]; // uso precomputato ?
            if (paths[v] > paths[u] + peso) {
                paths[v] = paths[u] + peso;
                pq.push({ paths[v], v });
            }
        }
    }

    return paths;
}

int main (int argc, char *argv[])
{
    try
    {
        // Set default input mesh filename
        //std::string filename("mallas/16Triangles.off");
        std::string filename("mallas/mannequin.ply");
        //std::string filename("mallas/knot-hole.ply");
        //std::string filename("mallas/Nefertiti.990kv.ply");
        //std::string filename("mallas/angel_kneeling.150kv.ply");

        if (argc > 1)
            filename = std::string(argv[1]);

        ///////////////////////////////////////////////////////////////////////
        //Read a mesh and write a mesh
        SimpleMesh mesh;        
        cout << "Loading file " << filename << endl;
        mesh.readFile(filename, false);

        cout << "Num vertex: " << mesh.numVertex() << " Num triangles: " << mesh.numTriangles()
             << " Unreferenced vertex: " << mesh.checkUnreferencedVertex() << endl;

        //Init time measures used for profiling
        high_resolution_clock::time_point clock0 = high_resolution_clock::now();

        ///////////////////////////////////////////////////////////////////////

        //Compute the list of external and internal edges
        //Implement the body of the updateEdgeLists() method .
        std::unordered_set <SimpleEdge, SimpleEdgeHash> externalEdges;
        std::unordered_set <SimpleEdge, SimpleEdgeHash> internalEdges;
        updateEdgeLists(mesh, externalEdges, internalEdges);
        

        cout << "Done updateEdgeLists() " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;
        cout << externalEdges.size() << " boundary edges" << endl;
        cout << internalEdges.size() << " internal edges" << endl;

        //Write external boundary and compute boundary length
        double boundaryLength = 0.0;
        std::cout << "Edges in the boundary:" << endl;
        for (const SimpleEdge &e : externalEdges)
        {
            std::cout << "[" << e.a << "->" << e.b << "] ";
            boundaryLength += mesh.edgeLength(e);
        }
        std::cout << endl;
        std::cout << "Boundary length: " << boundaryLength << endl;

        //Compute the Euler Characteristic for this mesh
        //For a conected mesh, the value means:
        //2 -> The mesh is a closed surface with no holes (sphere-like topology)
        //1 -> The mesh has one hole (disk-like topology)
        //0 -> The has one handle (torus-like topology)
        //-2 -> The mesh has two holes (tube-like topology)
        int eulerCharacteristic = mesh.numVertex() + mesh.numTriangles()
                                - externalEdges.size() - internalEdges.size();
        std::cout << "Euler Characteristic: " << eulerCharacteristic << endl;


        //Vector to store the distance from a vertex to the nearest vertex in the boundary
        std::vector<float> boundDist;
        //Index of the vertex with max distance to boundary
        unsigned deepestVertex = 0;

        //TODO 2.2:
        //Compute the distance from each vertex to the nearest vertex in the boundary (euclidean distance measured along edges)
        //and store it in the boundDist vector.
        //Store in deepestVertex the index of the vertex with the maximum distance to boundary
        
        std::vector<std::vector<int>> neighbours;
        mesh.computeNeighbours(neighbours);

        // Compute length between each vertex and its neighbours
        std::vector<std::vector<float>> edgeLengths(mesh.numVertex());
        for (size_t u = 0; u < mesh.numVertex(); ++u) {
            edgeLengths[u].resize(neighbours[u].size());
            for (size_t i = 0; i < neighbours[u].size(); ++i) {
                int v = neighbours[u][i];
                edgeLengths[u][i] = mesh.edgeLength(u, v); // calcolata UNA SOLA VOLTA
            }
        }

        std::unordered_set<int> externalVertexSet;
        for (const auto& e : externalEdges) {
            externalVertexSet.insert(e.a);
            externalVertexSet.insert(e.b);
        }
        std::vector<int> externalVertices(externalVertexSet.begin(), externalVertexSet.end());
         

        boundDist = Dijkstra(mesh.numVertex(), neighbours, edgeLengths, externalVertices);
      
        for (unsigned i = 0; i < boundDist.size(); ++i)
        {
            if (boundDist[i] > boundDist[deepestVertex])
            {
                deepestVertex = i;
                i++;
            }
        }
        //END TODO 2.2

        std::cout << "Done boundDist() " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;

        //Dump the index and distance for the deepestVertex
        std::cout << "maxDistance to boundary: " << boundDist[deepestVertex] << " at vertex: " << deepestVertex << endl;

        //Dump distances to boundary (for small meshes) for debugging
        if (boundDist.size() < 40)
        {
            cout << "Distances to boundary: " << endl;
            for (size_t i=0; i < boundDist.size(); i++)
                 cout << "vertex: " << i << " : " << boundDist[i] << endl;
            std::cout << endl;
        }

        /* Dump path from deepestVertex to nearest boundary vertex
        cout << "Shortest path from " << deepestVertex<< " to boundary:" << endl;
        int i = deepestVertex;
        while (i != -1)
        {
            cout << i << " -> ";
            i = parent[i];
        }
        cout << endl;
        //Dump path */

        std::string outputFilename="output_boundary.ply";
        std::cout << "Saving output to " << outputFilename << endl;


        //TODO 2.3:
        //Create a color mesh where the color of each vertex shows its distance to boundary
        //Save the color mesh to a PLY file named "output_boundary.ply"
        //See meshColor.cpp or meshColor2.cpp to see an how-to example

        //Create a color mesh where the color of each vertex shows its distance to boundary
        ColorMesh outputMesh;
        outputMesh.coordinates = mesh.coordinates;
        outputMesh.triangles = mesh.triangles;
        outputMesh.colors.resize(mesh.numVertex());

        for (size_t i = 0; i < mesh.numVertex(); i++)
        {
            outputMesh.colors[i].setTemperature(boundDist[deepestVertex] - boundDist[i], 0, boundDist[deepestVertex]); // min=0, val=t, max=1
        }
        //Save the color mesh to a PLY file named "output_boundary.ply"
        outputFilename = "C:/URJC/PracticaMFM/FMFIG-prac2025/output_boundary.ply";
        cout << "Saving output to " << outputFilename << endl;
        outputMesh.writeFilePLY(outputFilename);

        //See meshColor.cpp or meshColor2.cpp to see an how-to example

        //END TODO 2.3
        std::cout << "Done colorize by distance to boundary " << duration<float>(high_resolution_clock::now() - clock0).count() << " seconds" << endl;

        //Visualize the file with an external viewer
#ifdef WIN32
        //string viewcmd = "\"C:\\Program Files (x86)\\VCG\\MeshLab\\meshlab.exe\"";
        string viewcmd = "\"C:/Programmi/VCG/MeshLab/meshlab.exe\"";
#else
        string viewcmd = "meshlab >/dev/null 2>&1 ";
#endif
        string cmd = viewcmd+" "+outputFilename;
        std::cout << "Executing external command: " << cmd << endl;
        return system(cmd.c_str());
    }

    catch (const string &str) { std::cerr << "EXCEPTION: " << str << std::endl; }
    catch (const char *str) { std::cerr << "EXCEPTION: " << str << std::endl; }
    catch (std::exception& e)    { std::cerr << "EXCEPTION: " << e.what() << std::endl;  }
    catch (...) { std::cerr << "EXCEPTION (unknow)" << std::endl; }

#ifdef WIN32
    std::cout << "Press Return to end the program" <<endl;
    std::cin.get();
#else
#endif

    return 0;
}

