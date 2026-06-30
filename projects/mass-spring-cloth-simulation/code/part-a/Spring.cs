using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using VectorXD = MathNet.Numerics.LinearAlgebra.Vector<double>;
using MatrixXD = MathNet.Numerics.LinearAlgebra.Matrix<double>;
using DenseVectorXD = MathNet.Numerics.LinearAlgebra.Double.DenseVector;
using DenseMatrixXD = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix;

public class Spring : MonoBehaviour {

    #region InEditorVariables

    public float Stiffness;
    public Node nodeA;
    public Node nodeB;

    #endregion

    public float Length0;
    public float Length;

    private PhysicsManager Manager;

    // Update is called once per frame
    void Update() {

        Vector3 yaxis = new Vector3(0.0f, 1.0f, 0.0f);
        Vector3 dir = nodeA.Pos - nodeB.Pos;
        dir.Normalize();

        transform.position = 0.5f * (nodeA.Pos + nodeB.Pos);
        //The default length of a cylinder in Unity is 2.0
        transform.localScale = new Vector3(transform.localScale.x, Length / 2.0f, transform.localScale.z);
        transform.rotation = Quaternion.FromToRotation(yaxis, dir);
	}

    // Use this for initialization
    public void Initialize(PhysicsManager m)
    {
        Manager = m;

        UpdateState();
        Length0 = Length;
    }

    // Update spring state
    public void UpdateState()
    {
        Length = (nodeA.Pos - nodeB.Pos).magnitude;
    }

    // Get Force
    public void GetForce(VectorXD force)
    {
        UpdateState();
        if (Length < 0.0001f) return;

        Vector3 u = (nodeA.Pos - nodeB.Pos).normalized;

        // Elastic force
        Vector3 fElastic = -Stiffness * (Length - Length0) * u;

        force[nodeA.index] += fElastic.x;
        force[nodeA.index + 1] += fElastic.y;
        force[nodeA.index + 2] += fElastic.z;

        force[nodeB.index] -= fElastic.x;
        force[nodeB.index + 1] -= fElastic.y;
        force[nodeB.index + 2] -= fElastic.z;

        // Damping force
        // TO ADD
    }

    // Get Force Jacobian
    public void GetForceJacobian(MatrixXD dFdx)
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

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                double val = dFadxa[row, col];
                dFdx[ia + row, ia + col] += val;
                dFdx[ib + row, ib + col] += val;
                dFdx[ia + row, ib + col] -= val;
                dFdx[ib + row, ia + col] -= val;
            }
        }
    }

}
