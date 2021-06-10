using System.Collections.Generic;
using System.Linq;
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
    private ScalarValues sv;

    private void Awake()
    {
        Debug.Assert(lineContainer);

        obj = FindObjectOfType<WavefrontObjMesh>(true); Debug.Assert(obj);
        pcr = FindObjectOfType<PointCloudRenderer>(true); Debug.Assert(pcr);
    }

    private void OpenFile()
    {
        var filename = WinApi.FileOpenDialog("", "Load mesh", false
            , new WinApi.FileOpenDialogFilter[] { new WinApi.FileOpenDialogFilter("WaveFront obj", "*.obj") });
        if (filename == null) return; // canceled

        Debug.Log($"opening file : {filename}");
        obj.gameObject.SetActive(true);
        pcr.gameObject.SetActive(false);
        obj.Load(filename);
    }

    [Header("Pointclouds")]
    public bool drawSvdPlaneVectors;
    [aus.Property.ConditionalHide(nameof(drawSvdPlaneVectors))]
    public Transform lineContainer;

    private void CreatePointCloud()
    {
        obj.gameObject.SetActive(false);
        pcr.gameObject.SetActive(true);
        var mesh = obj.GetComponentInChildren<MeshFilter>().mesh; // TODO: 2 개 이상의 submesh 존재하는 경우 처리
        pc = new PointCloud(mesh);
        pcr.Setup(pc);

        if (drawSvdPlaneVectors) DrawSvdDirection();
    }

    public void Test()
    {
        ClearClusters();
        OpenFile();
        CreatePointCloud();
    }

    [Header("Geometric features")]
    public float radius = 0.03f;

    public void Test2()
    {
        if (pc == null)
        {
            Debug.LogWarning("NO point cloud data");
            return;
        }

        var min = float.MaxValue;
        var max = float.MinValue;

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
                var roughness = AnalysisTools.Roughness(p, neighbors);

                if (min > roughness) min = roughness;
                else if (max < roughness) max = roughness;
                values[i] = roughness;
            }
            );
        }
        Debug.Log($"roughness : min ={min}, max={max}");
        sv = new ScalarValues(values);

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
        if (t == 0) { Debug.LogError($"same values of roughness{min}"); return; }
        Parallel.For(0, count, (i) =>
        {
            var v = values[i];
            if (float.IsNaN(v)) { pcr.Colors[i] = Color.black; return; }
            pcr.Colors[i] = gradient.Evaluate((v - min) / t);
        });
        pcr.ApplyColors();
    }

    public void Test3()
    {
        if (pc == null)
        {
            Debug.LogWarning("NO point cloud data");
            return;
        }

        var min = float.MaxValue;
        var max = float.MinValue;

        var count = pc.Points.Length;
        var values = new float[count];
        using (new ElapsedTimeLogger("calculating normal change rate"))
        {
            Parallel.For(0, count, (i) =>
            {
                var p = pc.Points[i];
                var neighborIndices = pc.GetPointIndices(p, radius);
                if (neighborIndices.Count < 4) { values[i] = float.NaN; return; }

                var neighbors = pc.GetPoints(neighborIndices);
                var normalChangeRate = AnalysisTools.NormalChangeRate(neighbors);

                if (min > normalChangeRate) min = normalChangeRate;
                else if (max < normalChangeRate) max = normalChangeRate;
                values[i] = normalChangeRate;
            }
            );
        }
        Debug.Log($"normalChangeRate : min={min}, max={max}");
        sv = new ScalarValues(values);

        // visualize
        var gradient = new Gradient();

        // Populate the color keys at the relative time 0 and 1 (0 and 100%)
        var colorKeys = new GradientColorKey[] {
            new GradientColorKey { color = Color.blue, time = 0.0f },
            new GradientColorKey { color = Color.green, time = 0.33f },
            new GradientColorKey { color = Color.yellow, time = 0.66f },
            new GradientColorKey { color = Color.red, time = 1.0f },
        };
        var alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey { alpha = 1.0f, time = 0.0f },
            new GradientAlphaKey { alpha = 1.0f, time = 1.0f },
        };

        gradient.SetKeys(colorKeys, alphaKeys);

        var t = max - min;
        if (t == 0) { Debug.LogError($"same values of normal change rate{min}"); return; }
        Parallel.For(0, count, (i) =>
        {
            var v = values[i];
            if (float.IsNaN(v)) { pcr.Colors[i] = Color.black; return; }
            pcr.Colors[i] = gradient.Evaluate((v - min) / t);
        });
        pcr.ApplyColors();
    }

    [Header("Clustering")]
    public float meanWeight = 2.0f;

    private List<GameObject> clusterCenters = new List<GameObject>();
    private void ClearClusters()
    {
        foreach (var c in clusterCenters)
            Destroy(c);
    }
    public void Test4()
    {
        ClearClusters();

        if (sv == null)
        {
            Debug.LogWarning("no scalar values");
            return;
        }
        if (pc == null)
        {
            Debug.LogWarning("no poinc cluod");
            return;
        }
        Debug.Assert(pc.Points.Length == sv.Values.Length);

        var points = new List<Vector3>();
        for (var i = 0; i < pc.Points.Length; ++i)
        { // 평균값 이상의 point 들만 이용
            var val = sv.Values[i];
            if (float.IsNaN(val) || val < (sv.Mean * meanWeight)) continue;
            var pos = pc.Points[i];
            points.Add(pos);
        }

        // clustering by kmeans
        const int CLUSTER_COUNT = 3; // 양쪽 귀 + 코
        var samples = EmguCV.CreateMat(points, false);
        var criteria = new Emgu.CV.Structure.MCvTermCriteria(10, 0.01f); // 두가지 조건중 하나라도 도달하는 경우 완료
        var (_, _, centers) = samples.Kmeans(CLUSTER_COUNT, criteria);

        for (var i = 0; i < centers.Rows; ++i)
        {
            var pos = centers.GetVector3(i);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.transform.localScale = Vector3.one * 0.01f;
            marker.transform.position = pos;
            clusterCenters.Add(marker); // clear 하기위해 저장
        }
    }

    private void ClearLines()
    {
        foreach (Transform t in lineContainer.transform)
        {
            Destroy(t.gameObject);
        }
    }

    private static LineRenderer CreateLine(Vector3 pos1, Vector3 pos2, Color color)
    {
        var go = new GameObject("line");
        var line = go.AddComponent<LineRenderer>();
        var positions = new Vector3[] { pos1, pos2 };
        line.SetPositions(positions);
        line.material = new Material(Shader.Find("Particles/Standard Unlit"));
        line.startColor = color;
        line.endColor = color;
        line.startWidth = 0.01f;
        line.endWidth = 0.01f;
        return line;
    }

    private void DrawSvdDirection()
    {
        ClearLines();

        var center = pc.Bounds.center;
        lineContainer.transform.position = center;
        var centered = pc.Points.Select(x => x - center);
        var mat = EmguCV.CreateMat(centered);
        var svd = mat.ComputeSvd();

        var v0 = svd.U.GetRowVector3(0);
        var v1 = svd.U.GetRowVector3(1);
        var v2 = svd.U.GetRowVector3(2); // roughness
        DrawDirectionLine(v0, Color.red);
        DrawDirectionLine(v1, Color.green);
        DrawDirectionLine(v2, Color.blue);
        var sum = v0 * svd.W.GetFloatValue(0, 0) + v1 * svd.W.GetFloatValue(1, 0) + v2 * svd.W.GetFloatValue(2, 0);
        DrawDirectionLine(sum, Color.magenta);
    }

    private void DrawDirectionLine(Vector3 dir, Color color)
    {
        var line = CreateLine(lineContainer.position + Vector3.zero, lineContainer.position + dir.normalized, color);
        line.transform.parent = lineContainer;
        line.useWorldSpace = false;
        color *= 0.3f;
        line = CreateLine(lineContainer.position + Vector3.zero, lineContainer.position + (-dir.normalized), color);
        line.transform.parent = lineContainer;
        line.useWorldSpace = false;
    }
}