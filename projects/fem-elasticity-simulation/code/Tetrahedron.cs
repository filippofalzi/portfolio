using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using DenseMatrixXD = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix;
using DenseVectorXD = MathNet.Numerics.LinearAlgebra.Double.DenseVector;
using MatrixXD = MathNet.Numerics.LinearAlgebra.Matrix<double>;
using Triplet = System.Tuple<int, int, double>;
using VectorXD = MathNet.Numerics.LinearAlgebra.Vector<double>;

/// <summary>
/// Basic Tetrahedron class. Represents a single element of a linear
/// FEM discretization. Computes and stores the rest-state-depending
/// quantities and computes the corresponding strain energy, forces
/// and Jacobians necessary for simulation. It also allows to use
/// the well-known co-rotational model.
/// </summary>
public class Tetrahedron
{
    private List<Node> Nodes;
    private FEMSystem Fem;

    private MatrixXD B;
    private MatrixXD Bblock;
    private MatrixXD dPdF;
    private double Volume0;

    float Mu;
    float Lambda;

    private MatrixXD RotationOne;
    private MatrixXD RotationAll;

    private MatrixXD G;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="fem">The FEM simulable object that owns this tetrahedron</param>
    /// <param name="nodes">List of 4 nodes that define the vertices of the tetrahedron</param>
    /// 

    public Tetrahedron(FEMSystem fem, List<Node> nodes)
    {
        Fem = fem;
        Nodes = nodes;

        // Lamé parameters
        Mu = fem.GetMu();
        Lambda = fem.GetLambda();

        // Creating ISO_PARAMETRIC shape function derivatives
        G = new DenseMatrixXD(4, 3);

        G[0, 0] = -1; G[0, 1] = -1; G[0, 2] = -1;
        G[1, 0] = 1; G[1, 1] = 0; G[1, 2] = 0;
        G[2, 0] = 0; G[2, 1] = 1; G[2, 2] = 0;
        G[3, 0] = 0; G[3, 1] = 0; G[3, 2] = 1;


        // Compute PK1 matrix dP/dF in its linear version 
        dPdF = new DenseMatrixXD(9, 9);

        for (int i = 0; i < 9; i++) dPdF[i, i] = Mu;

        dPdF[1, 3] = Mu; dPdF[3, 1] = Mu; dPdF[2, 6] = Mu;
        dPdF[6, 2] = Mu; dPdF[5, 7] = Mu; dPdF[7, 5] = Mu; 
        
        dPdF[0, 0] += Mu + Lambda;
        dPdF[4, 4] += Mu + Lambda;
        dPdF[8, 8] += Mu + Lambda;
        // End dPdF filling

        // Precompute the part of the deformation gradient
        // that depends only on the undeformed configuration.
        ComputeB();

        // Set volume at rest
        Volume0 = ComputeVolume();

        // Compute initial rotation
        RotationOne = DenseMatrixXD.CreateIdentity(3);
        RotationAll = DenseMatrixXD.CreateIdentity(12);

        // Compute and distribute mass
        float mass = (float)Volume0 * Fem.Density;
        for (int i = 0; i < 4; ++i)
        {
            Nodes[i].Mass += mass / 4.0f;
        }
    }

    // Draw edges
    public void Draw()
    {
        for (int i = 0; i < 4; ++i)
            for (int j = i + 1; j < 4; ++j)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(Nodes[i].Pos, Nodes[j].Pos);
            }
    }

    /// <summary>
    /// Computes the rotation that best approximates the current state of
    /// the tetrahedron and stores it in the class variable RotationOne.
    /// Then, the diagonal 12x12 block matrix RotationAll is constructed
    /// to operate with the concatenation of tetrahedron nodes.
    /// </summary>
    public void UpdateRotation()
    {
        MatrixXD F = ComputeDeformationGradient();

        // Singular Value Decomposition: Decompose the deformation gradient in 
        // F = U*S*VT where U and and VT are orthogonal (rotation) matrix and S 
        // is a diagonal (scale) matrix. By recomputing F' = U*VT we are
        // elimitating the scale part.

        //Svd<double> factorSVD = F.Svd(true);
        //RotationOne = factorSVD.U * factorSVD.VT;

        // Gram-Schmidt orthogonalization: Computes the QR decomposition of the matrix
        // F = QR, where Q is an orthogonal (rotation) matrix and R is an upper triangular 
        // matrix. By keeping just Q we obtain an estimation of the rotation.

        GramSchmidt<double> factorGM = F.GramSchmidt();
        RotationOne = factorGM.Q;

        // Build the big block-diagonal rotation matrix.
        RotationAll = DenseMatrixXD.Create(12, 12, 0);
        for (int i = 0; i < 4; ++i)
            RotationAll.SetSubMatrix(3 * i, 3 * i, RotationOne);
    }

    /// <summary>
    /// Computes and assembles the elastic forces produced by this tetrahedron. 
    /// </summary>
    /// <param name="force">The globlal force vector where to assemble local force</param>
    public void GetForce(VectorXD force)
    {
        VectorXD localF = ComputeForce();

        for (int i = 0; i < 4; i++)
        {
            int id = Nodes[i].index;
            force[id + 0] += localF[i * 3 + 0];
            force[id + 1] += localF[i * 3 + 1];
            force[id + 2] += localF[i * 3 + 2];
        }
    }

    /// <summary>
    /// Computes and assembles the Jacobians of the elastic forces produced by this tetrahedron.
    /// </summary>
    /// <param name="DfDx">The global Jacobian matrix where to assemble local DfDx</param>
    /// <param name="DfDv">The global Jacobian matrix where to assemble local DfDv</param>
    public void GetJacobian(MatrixXD DfDx, MatrixXD DfDv)
    {

        // Questa funzione deve restituire: -Volume0 * R * K_local * R^T
        MatrixXD K_local = ComputeDfDx();

        // 2. Ciclo sulle righe (i) e colonne (j) della mattonella 12x12
        for (int i = 0; i < 12; i++)
        {
            for (int j = 0; j < 12; j++)
            {
                double val = K_local[i, j];

                if (val == 0) continue; // Pass to the next element if the current is zero

                // First divide by 3 to find out which node is among the 4 composing the tetrahedron
                // Then I look for its correspondent value in the global K matrix 
                //  (i % 3) is necessary to choose which coordinate to fill (x, y or z)
                int rowGlobale = Nodes[i / 3].index + (i % 3);
                int colGlobale = Nodes[j / 3].index + (j % 3);

                DfDx[rowGlobale, colGlobale] += val;
            }
        }
    }

    /// <summary>
    /// Computes and assembles the Jacobians of the elastic forces produced by this tetrahedron.
    /// </summary>
    /// <param name="DfDx">The global Jacobian matrix where to assemble local DfDx</param>
    /// <param name="DfDv">The global Jacobian matrix where to assemble local DfDv</param>
    public void GetJacobian(List<Triplet> DfDx, List<Triplet> DfDv)
    {
        MatrixXD K_local = ComputeDfDx();

        for (int i = 0; i < 12; i++)
        {
            for (int j = 0; j < 12; j++)
            {
                double val = K_local[i, j];
                if (val == 0) continue; // Pass to the next element if the current is zero

                // First divide by 3 to find out which node is among the 4 composing the tetrahedron
                // Then I look for its correspondent value in the global K matrix 
                //  (i % 3) is necessary to choose which coordinate to fill (x, y or z)
                int row = Nodes[i / 3].index + (i % 3);
                int col = Nodes[j / 3].index + (j % 3);

                DfDx.Add(new Triplet(row, col, val));
            }
        }
    }

    /// Private helper functions
    private double ComputeVolume()
    {
        Vector3 e0 = Nodes[1].Pos - Nodes[0].Pos;
        Vector3 e1 = Nodes[2].Pos - Nodes[0].Pos;
        Vector3 e2 = Nodes[3].Pos - Nodes[0].Pos;
        MatrixXD mV = new DenseMatrixXD(3);
        mV.SetColumn(0, Utils.ToVectorXD(e0));
        mV.SetColumn(1, Utils.ToVectorXD(e1));
        mV.SetColumn(2, Utils.ToVectorXD(e2));
        return (1.0 / 6.0) * mV.Determinant();
    }

    // Return the matrix Nx
    private MatrixXD GetNodeMatrix()
    {
        MatrixXD Nx = new DenseMatrixXD(3, 4);

        // Extracting actual (deformed) nodes configuraiton
        for (int i = 0; i < 4; i++)
        {

            Vector3 Node = Nodes[i].Pos;
            Nx[0, i] = Node.x;
            Nx[1, i] = Node.y;
            Nx[2, i] = Node.z;
        }

        return Nx;
    }

    // Initialize the matrices B and Bblock
    private void ComputeB()
    {
        B = new DenseMatrixXD(4, 3);
        Bblock = new DenseMatrixXD(9, 12);

        // Extracting initial Nodes matrix
        MatrixXD NX = new DenseMatrixXD(3, 4);

        for (int i = 0; i < 4; i++)
        {
            Vector3 Node = Nodes[i].Pos0;
            NX[0, i] = Node.x;
            NX[1, i] = Node.y;
            NX[2, i] = Node.z;
        }

        // Filling B matrix
        MatrixXD NXxG = new DenseMatrixXD(3, 3);
        NXxG.SetColumn(0, Utils.ToVectorXD(Nodes[1].Pos0 - Nodes[0].Pos0));
        NXxG.SetColumn(1, Utils.ToVectorXD(Nodes[2].Pos0 - Nodes[0].Pos0));
        NXxG.SetColumn(2, Utils.ToVectorXD(Nodes[3].Pos0 - Nodes[0].Pos0));

        B = G * NXxG.Inverse();

        // Filling Bblock matrix expanding B matrix
        for (int i = 0; i < 4; i++)
        {
            int col = i * 3;

            Bblock[0, col + 0] = B[i, 0];
            Bblock[1, col + 0] = B[i, 1];
            Bblock[2, col + 0] = B[i, 2];

            Bblock[3, col + 1] = B[i, 0];
            Bblock[4, col + 1] = B[i, 1];
            Bblock[5, col + 1] = B[i, 2];

            Bblock[6, col + 2] = B[i, 0];
            Bblock[7, col + 2] = B[i, 1];
            Bblock[8, col + 2] = B[i, 2];
        }
    }

    // Return F in matrix form
    private MatrixXD ComputeDeformationGradient()
    {
        MatrixXD F = new DenseMatrixXD(3, 3);

        VectorXD vecF = new DenseVectorXD(9);
        // Flattering nodes' coordinates
        VectorXD x = new DenseVectorXD(12);
        for (int i = 0; i < 4; i++)
        {
            x[i * 3 + 0] = Nodes[i].Pos.x;
            x[i * 3 + 1] = Nodes[i].Pos.y;
            x[i * 3 + 2] = Nodes[i].Pos.z;
        }

        // Computing vector F 
        vecF = Bblock * x;

        // Readjusting F to a matrix form
        for (int i = 0; i < 3; i++)
        {
            F[i, 0] = vecF[i * 3];
            F[i, 1] = vecF[i * 3 + 1];
            F[i, 2] = vecF[i * 3 + 2];
        }

        return F;
    }

    // Return the strain in matrix form
    private MatrixXD ComputeCauchyStrain()
    {
        MatrixXD E = new DenseMatrixXD(3, 3);

        // Computing strain
        MatrixXD eps = new DenseMatrixXD(3, 3);

        MatrixXD F = RotationOne.Transpose() * ComputeDeformationGradient();
        eps = 0.5 * (F + F.Transpose()) - DenseMatrixXD.CreateIdentity(3);

        E = 2 * Mu * eps + Lambda * eps.Trace() * DenseMatrixXD.CreateIdentity(3);
        return E;
    }

    // Return a vector with all node forces for this tetrahedron
    private VectorXD ComputeForce()
    {
        MatrixXD E = ComputeCauchyStrain();

        // Unrolling E into vecE
        VectorXD vecE = new DenseVectorXD(9);
        for (int i = 0; i < 3; i++) 
        {
            for (int j = 0; j < 3; j++) 
            {
                vecE[i * 3 + j] = E[i, j];
            }
        }

        // Computing local force
        VectorXD force = -Volume0 * RotationAll * Bblock.Transpose() * vecE;

        return force;
    }


    // Return a matrix with the Jacobian dfdx for this tetrahedron
    private MatrixXD ComputeDfDx()
    {
        MatrixXD jacobian = new DenseMatrixXD(12);
 
        // Gravity force --> dfg/dx = 0

        // f_int is the only contribution that must be calculated
        double V = ComputeVolume();

        jacobian = - Volume0 * RotationAll * Bblock.Transpose() * 
                   dPdF * Bblock * RotationAll.Transpose();

        return jacobian;
    }

    /*// Return the jacobian of dx_unrotated / dx_rotated
    private MatrixXD ComputeCorotJacobian()
    {
        MatrixXD jacobian = new DenseMatrixXD(12);
        MatrixXD Rblock = new DenseMatrixXD(12);
        MatrixXD Rt = RotationAll.Transpose();

        for(int i = 0; i < 4; i++)
        {
            Rblock.SetSubMatrix(i * 3, i * 3, Rt); // 0 3 6 9   
        }

        // For lienar FEM the jacobian is just Rblok
        return Rblock;
    }
    */
}

