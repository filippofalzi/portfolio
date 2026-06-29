/*
 * meshStatistic.cpp
 *
 * Written by Jose Miguel Espadero <josemiguel.espadero@urjc.es>
 *
 * This code is written as material for the FMF class of the
 * Master Universitario en Informatica Grafica, Juegos y Realidad Virtual.
 * Its purpose is to be didactic and easy to understand, not hard optimized.
 *
 * This file compute some statistics about a mesh and dump then to the console.
 * Also produce an output mesh with degenerate triangles remarked.
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
#include <iomanip>
#include <cstdlib>
#include <SimpleMesh.hpp>
#include <ColorMesh.hpp>
#include <limits>
#include <vector>
#include <numbers>
#include <algorithm>
#include <utility>
#include <chrono> // import C++11 high_resolution_clock for profiling
using namespace std::chrono;


// Set PI
const double pi = 3.14159265358979323846;

// Define structure containing 3 angles of a SimpleTriangle
struct Angles
{
    double a1, a2, a3;
};

//Find angles of a triangle
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

    //Compute |ca|·|cb|
    vec3 ca = A - C;
    vec3 cb = B - C;
    m = sqrt((ca * ca) * (cb * cb));
    cosine = (ca * cb) / m;
    sine = ca.cross(cb).module() / m;
    double angle3 = atan2(sine, cosine);

    return { angle1, angle2, angle3 };
};

// Compute the shaper factor of a triangle
double shapeFactor(const SimpleMesh& mesh, const SimpleTriangle& t)
{
    // Using SimpleMesh funcion --> edgeLength
    double la = mesh.edgeLength(t.a, t.b);
    double lb = mesh.edgeLength(t.b, t.c);
    double lc = mesh.edgeLength(t.c, t.a);

    double cr1 = la * lb * lc;
    if (cr1 == 0.0)
    {
        return 0.0;
    }

    double s = 0.5 * (la + lb + lc); //semiperimeter, used in circumradius
    double cr2 = 4 * sqrt(s * (la + lb - s) * (la + lc - s) * (lb + lc - s)); //circumradius pt2
    double minEdge = min(la, min(lb, lc));
    return (minEdge * cr2) / (cr1 * sqrt(3.0));
};

// Set red color
void vertSetRed(RGBColor& c) {
    c.r = 1.0f;
    c.g = 0.0f;
    c.b = 0.0f;
}


int main(int argc, char* argv[])
{
    try
    {
        //Set default input mesh filename
        std::string filename("mallas/AthenaBust.100kv.ply");
        if (argc > 1)
            filename = std::string(argv[1]);

        //Set default coordinate index to 0 (first vertex)
        unsigned long baseIndex = 0;
        if (argc > 2)
            baseIndex = strtoul(argv[2], nullptr, 10);

        ///////////////////////////////////////////////////////////////////////
        //Read a mesh from given filename
        ColorMesh mesh;
        std::cout << "Loading file " << filename << endl;
        mesh.readFile(filename);

        std::cout << "\nNum vertex: " << mesh.numVertex() << " Num triangles: " << mesh.numTriangles()
            << " Unreferenced vertex: " << mesh.checkUnreferencedVertex() << endl;

        //Compute minimum, maximum and average area per triangle, and total area for the mesh
        //double minArea, maxArea, averageArea = 0, totalArea;

        //Compute minimum and maximum angle for the mesh
        //double minAngle, maxAngle;

        //Compute minimum, maximum and average edge length
        //double minEdgeLen, maxEdgeLen, averageEdgeLen;

        //Compute minimum, maximum and average shape factor
        //double minShapeFactor, maxShapeFactor, averageShapeFactor;

        //////////////////////////////////////////////////////////////////////////////////
        //TODO 1.1:
        
        // Compute the values for minArea, maxArea, averageArea, totalArea
        std::vector<double> trAreas;
        double minArea = std::numeric_limits<double>::max();
        double maxArea = std::numeric_limits<double>::lowest();
        double averageArea = 0, totalArea = 0;

        // From SimpleMesh library: triangleArea(t)
        trAreas.reserve(mesh.triangles.size());
        size_t idx = 0; // for negative areas checking
        double area = 0;

        for (const auto& t : mesh.triangles)
        {
            area = mesh.triangleArea(t);

            // Check for negative area values
            //if (area < 0.0)
            //{
            //    std::cerr << "Error: triangle " << idx << " has negative area: " << area << std::endl;
            //}

            idx++;
            trAreas.push_back(area);
            totalArea += area;

            if (area < minArea)
            {
                minArea = area;
            }

            if (area > maxArea)
            {
                maxArea = area;
            }
        }
        averageArea = totalArea / mesh.triangles.size();

        // Compute the values for minAngle, maxAngle;
        double minAngle = INFINITY;
        double maxAngle = -INFINITY;
        idx = 0; // idx reset

        for (const auto& t : mesh.triangles)
        {
            Angles tAngles = computeAngle(mesh, t);

            //if (tAngles.a1 < 0 || tAngles.a2 < 0 || tAngles.a3 < 0)
            //{
            //    std::cerr << "Error: triangle " << idx << " has a negative angle";
            //}

            double currentMin = std::min({ tAngles.a1, tAngles.a2, tAngles.a3 });
            if (currentMin < minAngle)
            {
                minAngle = currentMin;
            }

            double currentMax = std::max({ tAngles.a1, tAngles.a2, tAngles.a3 });
            if (currentMax > maxAngle)
            {
                maxAngle = currentMax;
            }
            idx++;
        }

        // Compute the values for minEdgeLen, maxEdgeLen, averageEdgeLen;
        std::vector<double> edgeLengths; // Vector to store all edge lengths

        double minEdgeLen = INFINITY;
        double maxEdgeLen = -1 * INFINITY;
        double totalLength = 0;
        double averageEdgeLen = 0;

        // Loop over each triangle
        for (const auto& t : mesh.triangles) {
            // Compute the three edge lengths of the triangle
            // Using SimpleMesh funcion edgeLength
            double l1 = mesh.edgeLength(t.a, t.b);
            double l2 = mesh.edgeLength(t.b, t.c);
            double l3 = mesh.edgeLength(t.c, t.a);

            // Store edge lengths
            edgeLengths.push_back(l1);
            edgeLengths.push_back(l2);
            edgeLengths.push_back(l3);

            averageEdgeLen =

            // Update min and max
            minEdgeLen = std::min({ minEdgeLen, l1, l2, l3 });
            maxEdgeLen = std::max({ maxEdgeLen, l1, l2, l3 });

            // Accumulate total length for average
            totalLength += l1 + l2 + l3;
        }

        averageEdgeLen = totalLength / edgeLengths.size();

        //Set default degenerate triangle shapeFactor threshold
        double shapeFactorTh = 1 / (4 * sqrt(3));
        if (argc > 2)
        shapeFactorTh = strtod(argv[2], nullptr);

        // Compute the values for minShapeFactor, maxShapeFactor, averageShapeFactor;
        double minShapeFactor = 2, maxShapeFactor = -2, averageShapeFactor = 0;
        double umbral = 0.1443;
        double shFact = 2;
        const double epsilon = 1e-4;
        std::vector<std::pair<double, double>> smallShapeFactors;
        idx = 0;

        for (const auto& t : mesh.triangles)
        {
            shFact = shapeFactor(mesh, t);

            if (shFact > 1)
            {
                std::cerr << "Error, the triangle" << idx << "has shape facto" << shFact << ">1\n" << std::endl;
            }

            //Finding smallest shape factor
            if (shFact < minShapeFactor)
            {
                minShapeFactor = shFact;
            }
            //Finding greatest shape factor
            if (shFact > maxShapeFactor)
            {
                maxShapeFactor = shFact;
            }
            //Finding shape factor < umbral
            if (shFact <= umbral + epsilon)
            {
                smallShapeFactors.push_back({ idx, shFact });
            }
            averageShapeFactor += shFact;
            idx++;
        }
        averageShapeFactor = averageShapeFactor / mesh.triangles.size();
        //END TODO 1.1

        // Dump statistics to console output
        std::cout << std::fixed << std::setprecision(4) <<
            "\nArea    min: " << minArea <<
            " max: " << maxArea <<
            " average: " << averageArea <<
            " total: " << totalArea <<
            "\nAngle   min: " << minAngle <<
            " max: " << maxAngle <<
            "\nEdgeLen min: " << minEdgeLen <<
            " max: " << maxEdgeLen <<
            " average: " << averageEdgeLen <<
            "\nShapeF  min: " << minShapeFactor <<
            " max: " << maxShapeFactor <<
            " average: " << averageShapeFactor <<
            "\n\n" << smallShapeFactors.size() <<
            " trinagles have shape factor < 0.1443 " << endl;

        //////////////////////////////////////////////////////////////////////////////////
        //TODO OPTATIVE 1:
        //Compute Vertex area for each vertex and store in vertexAreas vector
        //Compute minimun and maximun vertex area in minVertexArea, maxVertexArea
        std::vector<double>vertexAreas(mesh.numVertex());
        double sumVertexAreas = 0;
        double minVertexArea = INFINITY;
        double maxVertexArea = 0;

        for (size_t i = 0; i < mesh.triangles.size(); ++i) {
            const auto& t = mesh.triangles[i];
            Angles tAngles = computeAngle(mesh, t);

            double ab = mesh.distance2(t.a, t.b); //distances squared
            double bc = mesh.distance2(t.b, t.c);
            double ca = mesh.distance2(t.c, t.a);

            if (tAngles.a1 > pi / 2)
            {
                vertexAreas[t.a] += mesh.triangleArea(t) / 2;
                vertexAreas[t.b] += mesh.triangleArea(t) / 4;
                vertexAreas[t.c] += mesh.triangleArea(t) / 4;
            }

            else if (tAngles.a2 > pi / 2)
            {
                vertexAreas[t.a] += mesh.triangleArea(t) / 4;
                vertexAreas[t.b] += mesh.triangleArea(t) / 2;
                vertexAreas[t.c] += mesh.triangleArea(t) / 4;
            }

            else if (tAngles.a3 > pi / 2)
            {
                vertexAreas[t.a] += mesh.triangleArea(t) / 4;
                vertexAreas[t.b] += mesh.triangleArea(t) / 4;
                vertexAreas[t.c] += mesh.triangleArea(t) / 2;
            }

            else {
                vertexAreas[t.a] += 1.0 / 8.0 * (ab / tan(tAngles.a3) + ca / tan(tAngles.a2));
                vertexAreas[t.b] += 1.0 / 8.0 * (ab / tan(tAngles.a3) + bc / tan(tAngles.a1));
                vertexAreas[t.c] += 1.0 / 8.0 * (ca / tan(tAngles.a2) + bc / tan(tAngles.a1));
            }
        }
        for (double area : vertexAreas) {
            sumVertexAreas += area;
            if (area < minVertexArea) minVertexArea = area;
            if (area > maxVertexArea) maxVertexArea = area;
        }

        //std::cout << "The total area of the Voronoi's cells is "
        //    << sumVertexAreas << "\n" << std::endl;
        
        //END TODO OPTATIVE 1

        //Check values for vertex areas and compute sum of areas:
        if (vertexAreas.size() != mesh.numVertex())
        {
            std::cout << "VertexAreas OPTATIVE PART DONE" << endl;
        }
        else
        {
            std::cout << "VertexA min: " << minVertexArea <<
                " max: " << maxVertexArea <<
                " average: " << sumVertexAreas / mesh.numVertex() <<
                " total: " << sumVertexAreas << "\n" << endl;
        }


        //////////////////////////////////////////////////////////////////////////////////
        //TODO 1.2:
        //Write triangles with ShapeFactor (radius / minEdge) greater than shapeFactorTh
        //Use messages formated as: "Triangle nnn has ShapeFactor xxx"
        for (const auto &s : smallShapeFactors)
        {
            std::cout << std::fixed << std::setprecision(4) <<
                "Triangle " << static_cast<int>(s.first) <<
                " has ShapeFactor " <<
                s.second << std::endl;
        }
        std::cout << "\n" << std::endl;
        //END TODO 1.2

        //////////////////////////////////////////////////////////////////////////////////
        //TODO 1.3:
        //Create a colorMesh where faces with ShapeFactor greater than shapeFactorTh
        //have their vertex colored in red. Save it to file named output_statistic.ply
        //and visualize it with meshlab or another external viewer.
        
        for (const auto& t : mesh.triangles)
        {
            shFact = shapeFactor(mesh, t);
            if (shFact < umbral)
            {
                // Color degenerate vertices in red    
                vertSetRed(mesh.colors[t.a]);
                vertSetRed(mesh.colors[t.b]);
                vertSetRed(mesh.colors[t.c]);
            }
        }
        //END TODO 1.3

        //Read the input mesh
        cout << "Loading file " << filename << endl;
        mesh.readFile(filename);

        //Prepare a copy of the input mesh on a ColorMesh
        ColorMesh outputMesh;
        outputMesh.coordinates = mesh.coordinates;
        outputMesh.triangles = mesh.triangles;
        outputMesh.colors.resize(mesh.numVertex());
        outputMesh.colors = mesh.colors;


        //Save result to a file in .ply format
        string outputFilename = "C:/URJC/PracticaMFM/output_meshStatistic.ply";
        outputMesh.writeFilePLY(outputFilename);

#ifdef WIN32
        string viewcmd = "\"C:/Programmi/VCG/MeshLab/meshlab.exe\"";
        //"\"C:/Archivos\ de\ programa/VCG/MeshLab/meshlab.exe\"";
        // C:\Program Files\VCG\MeshLab
        //string viewcmd = "C:/meshlab/meshlab_32.exe";
#else
        string viewcmd = "meshlab >/dev/null 2>&1 ";
#endif
        string cmd = viewcmd + " " + outputFilename;
        return system(cmd.c_str());
    }
    catch (const string& str) { std::cerr << "EXCEPTION: " << str << std::endl; }
    catch (const char* str) { std::cerr << "EXCEPTION: " << str << std::endl; }
    catch (std::exception& e) { std::cerr << "EXCEPTION: " << e.what() << std::endl; }
    catch (...) { std::cerr << "EXCEPTION (unknow)" << std::endl; }

#ifdef WIN32
    cout << "Press Return to end the program" << endl;
    std::cin.get();
#else
#endif
}
