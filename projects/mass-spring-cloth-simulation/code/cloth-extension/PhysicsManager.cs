using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra.Solvers;
using MathNet.Numerics.LinearAlgebra.Double.Solvers;


using VectorXD = MathNet.Numerics.LinearAlgebra.Vector<double>;
using MatrixXD = MathNet.Numerics.LinearAlgebra.Matrix<double>;
using DenseVectorXD = MathNet.Numerics.LinearAlgebra.Double.DenseVector;
using DenseMatrixXD = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix;

//using SparseMatrixXD = MathNet.Numerics.LinearAlgebra.Double.SparseMatrix;

/// <summary>
/// Basic physics manager capable of simulating a given ISimulable
/// implementation using diverse integration methods: explicit,
/// implicit, Verlet and semi-implicit.
/// </summary>
public class PhysicsManager : MonoBehaviour 
{
	/// <summary>
	/// Default constructor. Zero all. 
	/// </summary>
	public PhysicsManager()
	{
		Paused = true;
		TimeStep = 0.01f;
		Gravity = new Vector3 (0.0f, -9.81f, 0.0f);
		IntegrationMethod = Integration.Explicit;
	}

	/// <summary>
	/// Integration method.
	/// </summary>
	public enum Integration
	{
		Explicit = 0,
		Symplectic = 1,
        Implicit = 2,
	};

	#region InEditorVariables

	public bool Paused;
	public float TimeStep;
    public Vector3 Gravity;
    public List<GameObject> SimObjects;
    public List<Fixer> Fixers;
    public Integration IntegrationMethod;

    #endregion

    #region OtherVariables
    private List<ISimulable> m_objs;
    private int m_numDoFs;
    private MatrixXD M, K, D, A, Minv;
    private VectorXD x, v, f, b;    
    #endregion

    #region MonoBehaviour

    public void Start()
    {
 
        //Parse the simulable objects and initialize their state indices
        m_numDoFs = 0;
        m_objs = new List<ISimulable>(SimObjects.Capacity);

        foreach (GameObject obj in SimObjects)
        {
            ISimulable simobj = obj.GetComponent<ISimulable>();
            if (simobj != null)
            {
                m_objs.Add(simobj);

                // Initialize simulable object
                simobj.Initialize(m_numDoFs, this, Fixers);

                // Retrieve pos and vel size
                m_numDoFs += simobj.GetNumDoFs();
            }
        }

        M = new DenseMatrixXD(m_numDoFs, m_numDoFs);
        K = new DenseMatrixXD(m_numDoFs, m_numDoFs);
        D = new DenseMatrixXD(m_numDoFs, m_numDoFs);
        A = new DenseMatrixXD(m_numDoFs, m_numDoFs);
        Minv = new DenseMatrixXD(m_numDoFs, m_numDoFs);

        x = new DenseVectorXD(m_numDoFs);
        v = new DenseVectorXD(m_numDoFs);
        f = new DenseVectorXD(m_numDoFs);
        b = new DenseVectorXD(m_numDoFs);
    }

    public void Update()
	{
		if (Input.GetKeyUp (KeyCode.P))
			this.Paused = !this.Paused;

    }

    public void FixedUpdate()
    {
        if (this.Paused)
            return; // Not simulating

        // Select integration method
        switch (this.IntegrationMethod)
        {
            case Integration.Explicit: this.stepExplicit(); break;
            case Integration.Symplectic: this.stepSymplectic(); break;
            case Integration.Implicit: this.stepImplicit(); break;
            default:
                throw new System.Exception("[ERROR] Should never happen!");
        }
    }

    #endregion

    /// <summary>
    /// Performs a simulation step using Explicit integration.
    /// </summary>
    private void stepExplicit()
	{
        f.Clear();
        Minv.Clear();

        foreach (ISimulable obj in m_objs)
        {
            obj.GetPosition(x);
            obj.GetVelocity(v);
            obj.GetForce(f);
            obj.GetMassInverse(Minv);
        }

        foreach (ISimulable obj in m_objs)
        {
            obj.FixVector(f);
            obj.FixMatrix(Minv);
        }

        x += TimeStep * v;
        v += TimeStep * (Minv * f);

        foreach (ISimulable obj in m_objs)
        {
            obj.SetPosition(x);
            obj.SetVelocity(v);
        }
    }

    /// <summary>
    /// Performs a simulation step using Symplectic integration.
    /// </summary>
    private void stepSymplectic()
    {
        f.Clear();
        Minv.Clear();

        foreach (ISimulable obj in m_objs)
        {
            obj.GetPosition(x);
            obj.GetVelocity(v);
            obj.GetForce(f);
            obj.GetMassInverse(Minv);
        }

        foreach (ISimulable obj in m_objs)
        {
            obj.FixVector(f);
            obj.FixMatrix(Minv);
        }

        // Inverting the order to get Symplectic
        v += TimeStep * (Minv * f);
        x += TimeStep * v;

        foreach (ISimulable obj in m_objs)
        {
            obj.SetPosition(x);
            obj.SetVelocity(v);
        }
    }

    /// <summary>
    /// Performs a simulation step using Implicit integration.
    /// </summary>
    private void stepImplicit()
    {
        f.Clear();
        M.Clear();
        K.Clear();
        D.Clear();

        foreach (ISimulable obj in m_objs)
        {
            obj.GetPosition(x);
            obj.GetVelocity(v);
            obj.GetForce(f);
            obj.GetMass(M);
            obj.GetForceJacobian(K, D);
        }

        float h = TimeStep;
        float h2 = h * h;
        A = (MatrixXD)(M - h * D - h2 * K);
        b = (VectorXD)((M - h * D) * v + h * f);

        foreach (ISimulable obj in m_objs)
        {
            obj.FixVector(b);
            obj.FixMatrix(A);
        }

        /* VERSION CG STABLE
        var monitor = new Iterator<double>(
            new IterationCountStopCriterion<double>(20), 
            new ResidualStopCriterion<double>(1e-2)        
        );
        var preconditioner = new DiagonalPreconditioner();
        preconditioner.Initialize(A);

        var solver = new BiCgStab();
        VectorXD v_next = new DenseVectorXD(m_numDoFs);
        solver.Solve(A, b, v_next, monitor, preconditioner);
        v = v_next;
        x += h * v;
        */

        // VERSION LU SOLVER
        v = A.LU().Solve(b);
        x += h * v;

        foreach (ISimulable obj in m_objs)
        {
            obj.SetPosition(x);
            obj.SetVelocity(v);
        }
    }

}
