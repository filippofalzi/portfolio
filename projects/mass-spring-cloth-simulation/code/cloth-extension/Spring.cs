using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using VectorXD = MathNet.Numerics.LinearAlgebra.Vector<double>;
using MatrixXD = MathNet.Numerics.LinearAlgebra.Matrix<double>;
using DenseVectorXD = MathNet.Numerics.LinearAlgebra.Double.DenseVector;
using DenseMatrixXD = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix;

public class Spring {

    #region InEditorVariables

    public float Stiffness;
    public float Damping;
    public Node nodeA;
    public Node nodeB;

    #endregion

    public enum SpringType { Stretch, Bend };
    public SpringType springType;

    public float Length0;
    public float Length;
    public Vector3 dir;

    private PhysicsManager Manager;

    public Spring(Node a, Node b, SpringType s)
    {
        nodeA = a;
        nodeB = b;
        springType = s;
    }

    // Use this for initialization
    public void Initialize(float stiffness, float damping, PhysicsManager m)
    {
        Stiffness = stiffness;
        Damping = damping;
        Manager = m;
        Length0 = (nodeA.Pos - nodeB.Pos).magnitude; // Fondamentale!
    }

    // Update spring state
    public void UpdateState()
    {
        dir = nodeA.Pos - nodeB.Pos;
        Length = dir.magnitude;
        dir = (1.0f / Length) * dir;
    }

    // Get Force
    public void GetForce(VectorXD force)
    {
        UpdateState();
        if (Length < 0.0001f) return;

        Vector3 dir = (nodeA.Pos - nodeB.Pos).normalized;
        VectorXD u = new DenseVectorXD(new double[] { dir.x, dir.y, dir.z });
        MatrixXD uuT = (MatrixXD)u.OuterProduct(u);

        // Elastic force
        VectorXD fElastic = -Stiffness * (Length - Length0) * u;

        force[nodeA.index] += fElastic[0];
        force[nodeA.index + 1] += fElastic[1]; 
        force[nodeA.index + 2] += fElastic[2]; 

        force[nodeB.index] -= fElastic[0];
        force[nodeB.index + 1] -= fElastic[1];
        force[nodeB.index + 2] -= fElastic[2];

        // Damping force
        Vector3 vAB = nodeA.Vel - nodeB.Vel;
        VectorXD vDamping = new DenseVectorXD(new double[] { vAB.x, vAB.y, vAB.z });
        VectorXD fDamping = -Damping * uuT * vDamping;

        force[nodeA.index] += fDamping[0];
        force[nodeA.index + 1] += fDamping[1];
        force[nodeA.index + 2] += fDamping[2];

        force[nodeB.index] -= fDamping[0];
        force[nodeB.index + 1] -= fDamping[1];
        force[nodeB.index + 2] -= fDamping[2];
    }

    // Get Force Jacobian
    public void GetForceJacobian(MatrixXD dFdx, MatrixXD dFdv)
    {

        UpdateState();
        if (Length < 0.0001f) return;

        Vector3 dir = (nodeA.Pos - nodeB.Pos).normalized;
        VectorXD u = new DenseVectorXD(new double[] { dir.x, dir.y, dir.z });
        MatrixXD uuT = (MatrixXD)u.OuterProduct(u);
        MatrixXD I = DenseMatrixXD.CreateIdentity(3);
        int ia = nodeA.index;
        int ib = nodeB.index;

        MatrixXD dFadxa = -Stiffness * ((Length - Length0) / Length * (I - uuT) + uuT);
        MatrixXD dFadva = -Damping * uuT;

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                double val1 = dFadxa[row, col];
                dFdx[ia + row, ia + col] += val1;
                dFdx[ib + row, ib + col] += val1;
                dFdx[ia + row, ib + col] -= val1;
                dFdx[ib + row, ia + col] -= val1;

                double val2 = dFadva[row, col];
                dFdv[ia + row, ia + col] += val2;
                dFdv[ib + row, ib + col] += val2;
                dFdv[ia + row, ib + col] -= val2;
                dFdv[ib + row, ia + col] -= val2;
            }
        }
    }

}
