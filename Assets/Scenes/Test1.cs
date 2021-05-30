using MathNet.Numerics.LinearAlgebra;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using upc;
using upc.Component;

public class Test1 : MonoBehaviour
{
    private WavefrontObjMesh obj;
    private PointCloudRenderer pcr;

    private void Awake()
    {
        obj = FindObjectOfType<WavefrontObjMesh>(true); Debug.Assert(obj);
        pcr = FindObjectOfType<PointCloudRenderer>(true); Debug.Assert(pcr);
    }

    public void Test()
    {
        obj.gameObject.SetActive(false);
        var mesh = obj.GetComponentInChildren<MeshFilter>().mesh; // TODO: 2 개 이상의 submesh 존재하는 경우 처리
        pcr.Setup(mesh);
    }

    public void Test2()
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        var radius = 0.006f;

        var count = pcr.Points.Length;
        var values = new float[count];
        Parallel.For(0, count, (i) =>
        {
            var p = pcr.Points[i];
            var neighborIndecies = pcr.GetPointIndecies(p, radius);
            if (neighborIndecies.Count < 3) { values[i] = float.NaN; return; }
            var neighbors = pcr.GetPoints(neighborIndecies);
            var center = neighbors.GetMeanVector();

            var mat = Matrix<float>.Build.Dense(3, neighbors.Count, (r, c) => neighbors[c][r]);
            var svd = mat.Svd();
            var normal = new Vector3(svd.U.Column(2)[0], svd.U.Column(2)[1], svd.U.Column(2)[2]);
            var plane = new Plane(normal, center);
            var distance = Mathf.Abs(plane.GetDistanceToPoint(p));
            if (min > distance) min = distance;
            else if (max < distance) max = distance;
            values[i] = distance;
        });

        Debug.Log($"roughness : min={min}, max={max}");

        // visualize
        var gradient = new Gradient();

        // Populate the color keys at the relative time 0 and 1 (0 and 100%)
        var colorKey = new GradientColorKey[3];
        colorKey[0].color = Color.blue;
        colorKey[0].time = 0.0f;
        colorKey[1].color = Color.yellow;
        colorKey[1].time = 0.2f;
        colorKey[2].color = Color.red;
        colorKey[2].time = 1.0f;
        var alphaKey = new GradientAlphaKey[2];
        alphaKey[0].alpha = 1.0f;
        alphaKey[0].time = 0.0f;
        alphaKey[1].alpha = 1.0f;
        alphaKey[1].time = 1.0f;
        gradient.SetKeys(colorKey, alphaKey);

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
        var p = pcr.Points[samplIndex];
        var pis = pcr.GetPointIndecies(p, 0.02f);
        var points = pcr.GetPoints(pis);
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

    private void PlaneTest2()
    {
        var samplIndex = 20000;
        var p = pcr.Points[samplIndex];
        var pis = pcr.GetPointIndecies(p, 0.02f);
        var points = pcr.GetPoints(pis);
        Debug.Log($"points found = {pis.Count}");
        // coloring
        foreach (var pi in pis) pcr.Colors[pi] = Color.green;
        pcr.ApplyColors();

        // find bestfitting plane
        Plane(points, out var pos, out var normal);

        // visualize the plane
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.GetComponent<MeshRenderer>().material.doubleSidedGI = true;
        go.transform.up = normal;
        go.transform.position = pos;
    }

    public static void Line(List<Vector3> points, out Vector3 origin,
                            ref Vector3 direction, int iters = 100, bool drawGizmos = false)
    {
        if (
        direction == Vector3.zero ||
        float.IsNaN(direction.x) ||
        float.IsInfinity(direction.x)) direction = Vector3.up;

        //Calculate Average
        origin = Vector3.zero;
        for (int i = 0; i < points.Count; i++) origin += points[i];
        origin /= points.Count;

        // Step the optimal fitting line approximation:
        for (int iter = 0; iter < iters; iter++)
        {
            Vector3 newDirection = Vector3.zero;
            foreach (Vector3 worldSpacePoint in points)
            {
                Vector3 point = worldSpacePoint - origin;
                newDirection += Vector3.Dot(direction, point) * point;
            }
            direction = newDirection.normalized;
        }

        if (drawGizmos)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(origin, direction * 2f);
            Gizmos.DrawRay(origin, -direction * 2f);
        }
    }

    public static void Plane(List<Vector3> points, out Vector3 position,
      out Vector3 normal, int iters = 200, bool drawGizmos = false)
    {
        //Find the primary principal axis
        Vector3 primaryDirection = Vector3.right;
        Line(points, out position, ref primaryDirection, iters / 2, false);

        //Flatten the points along that axis
        List<Vector3> flattenedPoints = new List<Vector3>(points);
        for (int i = 0; i < flattenedPoints.Count; i++)
            flattenedPoints[i] = Vector3.ProjectOnPlane(points[i] - position, primaryDirection) + position;

        //Find the secondary principal axis
        Vector3 secondaryDirection = Vector3.right;
        Line(flattenedPoints, out position, ref secondaryDirection, iters / 2, false);

        normal = Vector3.Cross(primaryDirection, secondaryDirection).normalized;

        if (drawGizmos)
        {
            Gizmos.color = Color.red;
            foreach (Vector3 point in points) Gizmos.DrawLine(point, Vector3.ProjectOnPlane(point - position, normal) + position);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(position, normal * 0.5f); Gizmos.DrawRay(position, -normal * 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(position, Quaternion.LookRotation(normal, primaryDirection), new Vector3(1f, 1f, 0.001f));
            Gizmos.DrawWireSphere(Vector3.zero, 1f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}