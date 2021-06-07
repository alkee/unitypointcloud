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

    public void OpenFile()
    {
        var filename = WinApi.FileOpenDialog("", "Load mesh", false
            , new WinApi.FileOpenDialogFilter[] { new WinApi.FileOpenDialogFilter("WaveFront obj", "*.obj") });
        if (filename == null) return; // canceled

        Debug.Log($"opening file : {filename}");
        obj.gameObject.SetActive(true);
        pcr.gameObject.SetActive(false);
        obj.Load(filename);
    }

    public void Test()
    {
        obj.gameObject.SetActive(false);
        pcr.gameObject.SetActive(true);
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
                var roughness = AnalysisTools.Roughness(p, neighbors);

                if (min > roughness) min = roughness;
                else if (max < roughness) max = roughness;
                values[i] = roughness;
            }
            );
        }
        Debug.Log($"roughness : min ={min}, max={max}");

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
        var min = float.MaxValue;
        var max = float.MinValue;
        var radius = 0.03f;

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
                var normalChangeRate = AnalysisTools.NormalChangeRate(p, neighbors);

                if (min > normalChangeRate) min = normalChangeRate;
                else if (max < normalChangeRate) max = normalChangeRate;
                values[i] = normalChangeRate;
            }
            );
        }
        Debug.Log($"normalChangeRate : min={min}, max={max}");

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
}