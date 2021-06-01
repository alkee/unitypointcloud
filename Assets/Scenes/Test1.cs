using MathNet.Numerics.LinearAlgebra;
using System.Threading.Tasks;
using UnityEngine;
using upc;
using upc.Component;

public class Test1
    : MonoBehaviour
{
    private WavefrontObjMesh obj;
    private PointCloudRenderer pcr;

    private PointCloud pc;

    private void Awake()
    {
        obj = FindObjectOfType<WavefrontObjMesh>(true); Debug.Assert(obj);
        pcr = FindObjectOfType<PointCloudRenderer>(true); Debug.Assert(pcr);
    }

    public void Test()
    {
        obj.gameObject.SetActive(false);
        var mesh = obj.GetComponentInChildren<MeshFilter>().mesh; // TODO: 2 개 이상의 submesh 존재하는 경우 처리
        pc = new PointCloud(mesh);
        pcr.Setup(pc);
    }

    public void Test2()
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        var radius = 0.006f;

        var count = pc.Points.Length;
        var values = new float[count];
        using (new ElapsedTimeLogger("calculating roughness"))
        {
            Parallel.For(0, count, (i) =>
            {
                var p = pc.Points[i];
                var neighborIndices = pc.GetPointIndices(p, radius);
                if (neighborIndices.Count < 3) { values[i] = float.NaN; return; }
                var neighbors = pc.GetPoints(neighborIndices);
                var center = neighbors.GetMeanVector();

                var mat = Matrix<float>.Build.Dense(3, neighbors.Count, (r, c) => neighbors[c][r]);
                var svd = mat.Svd();
                var normal = new Vector3(svd.U.Column(2)[0], svd.U.Column(2)[1], svd.U.Column(2)[2]);
                var plane = new Plane(normal, center);
                var distance = Mathf.Abs(plane.GetDistanceToPoint(p));
                if (min > distance) min = distance;
                else if (max < distance) max = distance;
                values[i] = distance;
            }
            );
        }
        Debug.Log($"roughness : min={min}, max={max}");

        // visualize
        var gradient = new Gradient();

        // Populate the color keys at the relative time 0 and 1 (0 and 100%)
        var colorKeys = new GradientColorKey[] {
            new GradientColorKey { color = Color.blue, time = 0.0f },
            new GradientColorKey { color = Color.green, time = 0.4f },
            new GradientColorKey { color = Color.yellow, time = 0.6f },
            new GradientColorKey { color = Color.red, time = 1.0f },
        };
        var alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey { alpha = 1.0f, time = 0.0f },
            new GradientAlphaKey { alpha = 1.0f, time = 1.0f },
        };

        gradient.SetKeys(colorKeys, alphaKeys);

        var t = max - min;
        Parallel.For(0, count, (i) =>
        {
            var v = values[i];
            if (float.IsNaN(v)) { pcr.Colors[i] = Color.black; return; }
            pcr.Colors[i] = gradient.Evaluate((v - min) / t);
        });
        pcr.ApplyColors();
    }

    private void PlaneTest1()
    {
        var samplIndex = 20000;
        var p = pc.Points[samplIndex];
        var pis = pc.GetPointIndices(p, 0.02f);
        var points = pc.GetPoints(pis);
        Debug.Log($"points found = {pis.Count}");
        // coloring
        foreach (var pi in pis) pcr.Colors[pi] = Color.green;
        pcr.ApplyColors();

        // find bestfitting plane
        var mat = Matrix<float>.Build.Dense(3, points.Count, (r, c) => points[c][r]);
        var svd = mat.Svd();
        var normal = new Vector3(svd.U.Column(2)[0], svd.U.Column(2)[1], svd.U.Column(2)[2]);
        var pos = points.GetMeanVector();

        // visualize the plane
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.GetComponent<MeshRenderer>().material.doubleSidedGI = true;
        go.transform.up = normal;
        go.transform.position = pos;
    }
}