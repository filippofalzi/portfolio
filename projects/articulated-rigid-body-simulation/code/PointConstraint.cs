using System.Collections;
using System.Collections.Generic;
//using UnityEditor.PackageManager;
using UnityEngine;
//using static UnityEditor.ShaderData;
using DenseMatrixXD = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix;
using DenseVectorXD = MathNet.Numerics.LinearAlgebra.Double.DenseVector;
using MatrixXD = MathNet.Numerics.LinearAlgebra.Matrix<double>;
using VectorXD = MathNet.Numerics.LinearAlgebra.Vector<double>;

/// <summary>
/// Basic point constraint between two rigid bodies.
/// </summary>
public class PointConstraint : MonoBehaviour, IConstraint
{
    /// <summary>
    /// Default constructor. All zero. 
    /// </summary>
    public PointConstraint()
    {
        Manager = null;
    }

    #region EditorVariables

    public float Stiffness;

    public RigidBody bodyA;
    public RigidBody bodyB;

    #endregion

    #region OtherVariables

    int index;
    private PhysicsManager Manager;

    protected Vector3 pointA;
    protected Vector3 pointB;

    #endregion

    #region MonoBehaviour

    // Update is called once per frame
    void Update()
    {
        // Compute the average position
        Vector3 posA = (bodyA != null) ? bodyA.PointLocalToGlobal(pointA) : pointA;
        Vector3 posB = (bodyB != null) ? bodyB.PointLocalToGlobal(pointB) : pointB;
        Vector3 pos = 0.5f * (posA + posB);

        // Apply the position
        Transform xform = GetComponent<Transform>();
        xform.position = pos;
    }

    #endregion

    #region IConstraint

    public void Initialize(int ind, PhysicsManager m)
    {
        index = ind;
        Manager = m;

        // Initialize local positions. We assume that the object is connected to a Sphere mesh.
        Transform xform = GetComponent<Transform>();
        if (xform == null)
        {
            System.Console.WriteLine("[ERROR] Couldn't find any transform to the constraint");
        }
        else
        {
            System.Console.WriteLine("[TRACE] Succesfully found transform connected to the constraint");
        }

        // Initialize kinematics
        Vector3 pos = xform.position;

        // Local positions on objects
        pointA = (bodyA != null) ? bodyA.PointGlobalToLocal(pos) : pos;
        pointB = (bodyB != null) ? bodyB.PointGlobalToLocal(pos) : pos;

    }

    public int GetNumConstraints()
    {
        return 3;
    }

    public void GetConstraints(VectorXD c)
    {
        Vector3 posA = (bodyA != null) ? bodyA.PointLocalToGlobal(pointA) : pointA;
        Vector3 posB = (bodyB != null) ? bodyB.PointLocalToGlobal(pointB) : pointB;
        Vector3 C_x = posA - posB;
        c.SetSubVector(index, 3, Utils.ToVectorXD(C_x));
    }

    public void GetConstraintJacobian(MatrixXD dcdx)
    {
        if (bodyA != null)
        {
            // Setting the derivatives of mass center (dCdxa and dCdxa) in the Matrix
            dcdx.SetSubMatrix(index, bodyA.index, DenseMatrixXD.CreateIdentity(3));
            
            // Calculating separately the derivatives of the angular displacement
            MatrixXD dCdthetaA = -Utils.Skew(bodyA.PointLocalToGlobal(pointA) - bodyA.m_pos);
            dcdx.SetSubMatrix(index, bodyA.index + 3, dCdthetaA);
        }

        if (bodyB != null)
        {
            dcdx.SetSubMatrix(index, bodyB.index, -DenseMatrixXD.CreateIdentity(3));

            MatrixXD dCdthetaB = Utils.Skew(bodyB.PointLocalToGlobal(pointB) - bodyB.m_pos);
            dcdx.SetSubMatrix(index, bodyB.index + 3, dCdthetaB);
        }
    }

    public void GetForce(VectorXD force)
    {
        Vector3 posA = (bodyA != null) ? bodyA.PointLocalToGlobal(pointA) : pointA;
        Vector3 posB = (bodyB != null) ? bodyB.PointLocalToGlobal(pointB) : pointB;
        VectorXD c = new DenseVectorXD(GetNumConstraints());
        c = Utils.ToVectorXD(posA - posB);
      
        if (bodyA != null)
        {
            MatrixXD dcdqA = new DenseMatrixXD(GetNumConstraints(), 6);
            GetPointJacobian(dcdqA, bodyA);

            // Calculate the forces and torques   
            VectorXD Fa = -Stiffness * dcdqA.Transpose() * c;
            // Filling the matrix
            force.SetSubVector(bodyA.index, 6, force.SubVector(bodyA.index, 6).Add(Fa));
        }

        if (bodyB != null)
        {
            MatrixXD dcdqB = new DenseMatrixXD(GetNumConstraints(), 6);
            GetPointJacobian(dcdqB, bodyB);

            VectorXD Fb = -Stiffness * dcdqB.Transpose() * c;
            force.SetSubVector(bodyB.index, 6, force.SubVector(bodyB.index, 6).Add(Fb));
        }
    }

    public void GetForceJacobian(MatrixXD dFdx, MatrixXD dFdv)
    {
        MatrixXD jA = new DenseMatrixXD(3, 6);
        MatrixXD jB = new DenseMatrixXD(3, 6);

        if (bodyA != null) GetPointJacobian(jA, bodyA);
        if (bodyB != null) GetPointJacobian(jB, bodyB);

        // Objective:  -k * grad(C) * grad(C)^T
        // Let's use GetPoinJacobian to fill the submatrices of the global matrix

        // dFdx must be 6 * numBodies x 6 * numBodies --> 12 x 12
        // A(m x n) * B (n x m) --> C(m x m) respect matrix dimensions 
      
        // Calculating and inserting diagonal bloques --> matrices 6 x 6
        // dFadxa
        if (bodyA != null)
        {
            MatrixXD K_AA = -Stiffness * (jA.Transpose() * jA);
            dFdx.SetSubMatrix(bodyA.index, bodyA.index, dFdx.SubMatrix(bodyA.index, 6, bodyA.index, 6).Add(K_AA));
        }

        // dFbdxb
        if (bodyB != null)
        {
            MatrixXD K_BB = -Stiffness * (jB.Transpose() * jB);
            dFdx.SetSubMatrix(bodyB.index, bodyB.index, dFdx.SubMatrix(bodyB.index, 6, bodyB.index, 6).Add(K_BB));
        }

        // Calculating and inserting bloques out of the diagonal --> matrices 6 x 6
        if (bodyA != null && bodyB != null)
        {
            // dFadxb
            MatrixXD K_AB = (MatrixXD)(-Stiffness * (jA.Transpose() * jB));
            dFdx.SetSubMatrix(bodyA.index, bodyB.index, dFdx.SubMatrix(bodyA.index, 6, bodyB.index, 6).Add(K_AB));

            // dFbdxa
            MatrixXD K_BA = (MatrixXD)(-Stiffness * (jB.Transpose() * jA));
            dFdx.SetSubMatrix(bodyB.index, bodyA.index, dFdx.SubMatrix(bodyB.index, 6, bodyA.index, 6).Add(K_BA));
        }

        // The constraints does not have a damping effect --> dFdv remains untouched
    }

    public void GetPointJacobian(MatrixXD dcdq, RigidBody body)
    {
        // Check if the body is null --> initial constraint
        if (body == null) return;

        // Calculatin dcdx.body
        dcdq.SetSubMatrix(0, 0,  (body == bodyA) ? DenseMatrixXD.CreateIdentity(3) : -DenseMatrixXD.CreateIdentity(3));

        // Calculating dcdtheta.body
        MatrixXD dcdtheta;

        if (body == bodyA)
        {
            dcdtheta = -Utils.Skew(bodyA.PointLocalToGlobal(pointA) - bodyA.m_pos);
        }
        else
        {
            dcdtheta = Utils.Skew(bodyB.PointLocalToGlobal(pointB) - bodyB.m_pos);
        }

        // Inserting the sub matrix in the global matrix
        dcdq.SetSubMatrix(0, 3, dcdtheta);
    }

    #endregion

}
