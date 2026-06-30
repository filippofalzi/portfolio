using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using VectorXD = MathNet.Numerics.LinearAlgebra.Vector<double>;
using MatrixXD = MathNet.Numerics.LinearAlgebra.Matrix<double>;
using DenseVectorXD = MathNet.Numerics.LinearAlgebra.Double.DenseVector;
using DenseMatrixXD = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix;

/// <summary>
/// Basic mass-spring model component which can be dropped onto
/// a game object and configured so that the set of nodes and
/// edges behave as a mass-spring model.
/// </summary>
public class MassSpring : MonoBehaviour, ISimulable
{
    /// <summary>
    /// Default constructor. All zero. 
    /// </summary>
    public MassSpring()
    {
        Manager = null;
    }

    #region EditorVariables

    public List<Node> Nodes;
    public List<Spring> Springs;

    public float Mass;
    public float StiffnessStretch;
    public float StiffnessBend;
    public float DampingAlpha;
    public float DampingBeta;

    #endregion

    #region OtherVariables
    private PhysicsManager Manager;
    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    int[] nodesToVertex;

    private int index;
    #endregion

    #region MonoBehaviour

    public void Awake()
    {
        mesh = this.GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;

        Nodes = new List<Node>(vertices.Length);
        Springs = new List<Spring>(vertices.Length * 6);

        Dictionary<Vector3, int> nodesList = new Dictionary<Vector3, int>();
        Dictionary<string, int> springsList = new Dictionary<string, int>();
        nodesToVertex = new int[vertices.Length];

        // Create nodes
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 globalPos = transform.TransformPoint(vertices[i]);
           
            if (!nodesList.ContainsKey(globalPos))
            {
                Node new_node = new Node(globalPos);
                Nodes.Add(new_node);
                nodesList.Add(globalPos, Nodes.Count - 1);
                nodesToVertex[i] = Nodes.Count - 1;
            }
            else
            {
                nodesToVertex[i] = nodesList[globalPos];
            }

        }

        // Create spirngs
        for (int i = 0; i < triangles.Length; i += 3)
        {
            for (int s = 0; s < 3; s++)
            {
                int a = triangles[i + s];
                int b = triangles[i + (s + 1) % 3];
                int c = triangles[i + (s + 2) % 3];

                int nodeA = nodesToVertex[a];
                int nodeB = nodesToVertex[b];
                int nodeC = nodesToVertex[c];

                string key = Mathf.Min(nodeA, nodeB) + "-" + Mathf.Max(nodeA, nodeB);

                if (!springsList.ContainsKey(key))
                {
                    Spring new_spring = new Spring(Nodes[nodeA], Nodes[nodeB], Spring.SpringType.Stretch);
                    Springs.Add(new_spring);

                    springsList.Add(key, nodeC);
                }

                else
                {
                    if (springsList[key] != nodeC)
                    {
                        Spring new_spring = new Spring(Nodes[springsList[key]], Nodes[nodeC], Spring.SpringType.Bend);
                        Springs.Add(new_spring);
                    }
                }
            }
        }
    }

    public void Update()
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            int nodeInd = nodesToVertex[i];
            vertices[i] = transform.InverseTransformPoint(Nodes[nodeInd].Pos);
        }

        mesh.vertices = vertices;
    }

    public void FixedUpdate()
    {
       
    }
    #endregion

    #region ISimulable

    public void Initialize(int ind, PhysicsManager m, List<Fixer> fixers)
    {
        float massParticle = Mass / Nodes.Count;
        Manager = m;
        index = ind;

        for (int i = 0; i < Nodes.Count; ++i) {
            Nodes[i].Initialize(ind + 3*i, massParticle, DampingAlpha, Manager);

            foreach (Fixer f in fixers){
               
                if (f.IsInside(Nodes[i].Pos))
                    Nodes[i].Fixed = true;
            }
        }
        
        for (int i = 0; i < Springs.Count; ++i) {
            
            if (Springs[i].springType == Spring.SpringType.Stretch)
                Springs[i].Initialize(StiffnessStretch, DampingBeta, Manager);

            if (Springs[i].springType == Spring.SpringType.Bend)
                Springs[i].Initialize(StiffnessBend, DampingBeta, Manager);
        }
    }

    public int GetNumDoFs()
    {
        return 3 * Nodes.Count;
    }

    public void GetPosition(VectorXD position)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].GetPosition(position);
    }

    public void SetPosition(VectorXD position)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].SetPosition(position);
        for (int i = 0; i < Springs.Count; ++i)
            Springs[i].UpdateState();
    }

    public void GetVelocity(VectorXD velocity)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].GetVelocity(velocity);
    }

    public void SetVelocity(VectorXD velocity)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].SetVelocity(velocity);
    }

    public void GetForce(VectorXD force)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].GetForce(force);
        for (int i = 0; i < Springs.Count; ++i)
            Springs[i].GetForce(force);
    }

    public void GetForceJacobian(MatrixXD dFdx, MatrixXD dFdv)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].GetForceJacobian(dFdx, dFdv);
        for (int i = 0; i < Springs.Count; ++i)
            Springs[i].GetForceJacobian(dFdx, dFdv);
    }

    public void GetMass(MatrixXD mass)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].GetMass(mass);
    }

    public void GetMassInverse(MatrixXD massInv)
    {
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].GetMassInverse(massInv);
    }

    public void FixVector(VectorXD v)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            Nodes[i].FixVector(v);
        }
    }

    public void FixMatrix(MatrixXD M)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            Nodes[i].FixMatrix(M);
        }
    }

    #endregion

    #region OtherMethods

    #endregion

}
