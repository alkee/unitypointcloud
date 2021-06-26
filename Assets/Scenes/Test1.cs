using System;
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
        obj.Load(filename);
    }

    [Header("Pointclouds")]
    public bool drawSvdPlaneVectors;
    [aus.Property.ConditionalHide(nameof(drawSvdPlaneVectors))]
    public Transform lineContainer;

    private void CreatePointCloud()
    {
        var mesh = obj.DefaultMeshFilter.mesh; // TODO: 2 개 이상의 submesh 존재하는 경우 처리
        pc = new PointCloud(mesh);
        pcr.Setup(pc);

        obj.gameObject.SetActive(false);
        pcr.gameObject.SetActive(true);

        if (drawSvdPlaneVectors) DrawSvdDirection();
    }

    private static void ColorizeScalarField(PointCloudRenderer target, ScalarValues sv, Func<int, float, float> filter = null)
    {
        if (filter == null) filter = (i, x) => x;
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

        var t = sv.Max - sv.Min;
        if (t == 0) { Debug.LogError($"same values of roughness{sv.Min}"); return; }
        Parallel.For(0, sv.Values.Length, (i) =>
        {
            var v = filter(i, sv.Values[i]);
            if (float.IsNaN(v)) { target.Colors[i] = Color.black; return; }
            target.Colors[i] = gradient.Evaluate((v - sv.Min) / t);
        });
        target.ApplyColors();
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
        ColorizeScalarField(pcr, sv);
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
        ColorizeScalarField(pcr, sv);
    }

    public void CalculateEffectiveEnergy()
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
        using (new ElapsedTimeLogger("calculating Effective Energy"))
        {
            Parallel.For(0, count, (i) =>
            {
                var p = pc.Points[i];
                var neighborIndices = pc.GetPointIndices(p, radius);

                var neighbors = pc.GetPoints(neighborIndices);
                var eeS = AnalysisTools.EffectiveEnergy(p, pc.Normals[i], neighbors);
                var ee = eeS.u * eeS.u * eeS.v;
                if (eeS.u < 0) ee *= 4.0f;
                //var d = (pc.Bounds.center - p).sqrMagnitude;
                //ee *= 1 / d;

                if (min > ee) min = ee;
                else if (max < ee) max = ee;
                values[i] = ee;
            }
            );
        }
        Debug.Log($"Effective energy : min={min}, max={max}");
        sv = new ScalarValues(values);

        // visualize
        ColorizeScalarField(pcr, sv);
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

        // visualize
        ColorizeScalarField(pcr, sv, (i, v) => v < (sv.Mean * meanWeight) ? float.NaN : v);
        for (var i = 0; i < centers.Rows; ++i)
        {
            var pos = centers.GetVector3(i);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.transform.localScale = Vector3.one * 0.01f;
            marker.transform.position = pos;
            clusterCenters.Add(marker); // clear 하기위해 저장
        }
    }

    public void Test5()
    {
        Dnn();
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

    [Header("CascadeClassifier")]
    public UnityEngine.UI.RawImage imageTarget;
    public string classifierFilename;

    private void CascadeClassifier()
    {
        if (System.IO.File.Exists(classifierFilename) == false)
        {
            Debug.LogWarning($"file not found : {classifierFilename}");
            return;
        }

        obj.gameObject.SetActive(true);
        pcr.gameObject.SetActive(false);

        if (!imageTarget) imageTarget = FindObjectOfType<UnityEngine.UI.RawImage>(true);
        imageTarget.gameObject.SetActive(false); // 다시 실행되는 경우 

        var tex = Camera.main.Render(640, 480, RenderTextureFormat.ARGB32);
        var mat = EmguCV.CreateCvImage(tex);

        var c = new Emgu.CV.CascadeClassifier(classifierFilename);
        var rs = c.DetectMultiScale(mat);
        if (rs.Length == 0) Debug.LogWarning("no feature detected");

        foreach (var r in rs)
        {
            // texture 는 bottom-left 가 (0,0) mat 은 top-left 가 (0,0)
            var rect = new RectInt(r.Left, tex.height - r.Top - r.Height, r.Width, r.Height);
            Debug.Log($"(x={r.X}, y={r.Y}, w={r.Width}, h={r.Height} ==> to {rect}");
            tex.DrawRect(rect, new Color(0.0f, 1.0f, 0.0f, 0.3f));
        }
        tex.Apply();

        // visualize
        imageTarget.texture = tex;
        imageTarget.gameObject.SetActive(true);
    }

    [Header("Dnn")]
    public string modelProtoTxt; // deploy.prototxt
    public string caffeModel; // res10_300x300_ssd_iter_140000.caffemodel

    private void Dnn()
    {
        // https://www.pyimagesearch.com/2018/02/26/face-detection-with-opencv-and-deep-learning/
        // https://github.com/emgucv/emgucv/blob/7b824371fd93f37296efa073c56d399f57d178d6/Emgu.CV.Test/AutoTestVarious.cs#L3681
        if (System.IO.File.Exists(modelProtoTxt) == false)
        {
            Debug.LogWarning($"file not found : {modelProtoTxt}");
            return;
        }
        if (System.IO.File.Exists(caffeModel) == false)
        {
            Debug.LogWarning($"file not found : {caffeModel}");
            return;
        }

        obj.gameObject.SetActive(true);
        pcr.gameObject.SetActive(false);

        if (!imageTarget) imageTarget = FindObjectOfType<UnityEngine.UI.RawImage>(true);
        imageTarget.gameObject.SetActive(false); // 다시 실행되는 경우 

        var tex = Camera.main.Render(640, 480, RenderTextureFormat.ARGB32);
        var img = EmguCV.CreateCvImage(tex);

        Debug.Log($"image size = {img.Size}, dim = {img.Mat.Dims}, channel = {img.NumberOfChannels}, elementSize = {img.Mat.ElementSize}");

        var net = Emgu.CV.Dnn.DnnInvoke.ReadNetFromCaffe(modelProtoTxt, caffeModel);
        var blob = Emgu.CV.Dnn.DnnInvoke.BlobFromImage(img);
        net.SetInput(blob, "data");
        
        var detections = net.Forward();

        var confidenceThreshold = 0.5f;
        var rects = new List<RectInt>();

        var dimSizes = detections.SizeOfDimension;
        var step = dimSizes[3] * sizeof(float);
        var start = detections.DataPointer;

        for(var i=0;i<dimSizes[2]; i++)
        {
            var values = new float[dimSizes[3]];
            System.Runtime.InteropServices.Marshal.Copy(new IntPtr(start.ToInt64() + step * i), values, 0, dimSizes[3]);
            var confident = values[2];
            if (confident < confidenceThreshold) continue;

            float xLeftBottom = values[3] * img.Rows;
            float yLeftBottom = values[4] * img.Cols;
            float xRightTop = values[5] * img.Rows;
            float yRightTop = values[6] * img.Cols;

            int xMin = Mathf.RoundToInt(xLeftBottom);
            int yMin = Mathf.RoundToInt(yLeftBottom);
            int width = Mathf.RoundToInt(xRightTop - xLeftBottom);
            int height = Mathf.RoundToInt(yRightTop - yLeftBottom);

            // Texture2D 는 bottom-left 가 (0,0) mat 은 top-left 가 (0,0)
            //var faceRegion = new RectInt(xMin, yMin, width, height);
            var faceRegion = new RectInt(xMin, img.Cols - yMin - height, width, height);

            Debug.Log($"{values[0]} {values[1]} c:{values[2]} / {values[3]}, {values[4]}, {values[5]}, {values[6]} => {faceRegion:F4}");


            rects.Add(faceRegion);
        }

        foreach (var r in rects)
        {
            tex.DrawRect(r, new Color(0.0f, 1.0f, 0.0f, 0.3f));
        }

        // visualize
        tex.Apply();
        imageTarget.texture = tex;
        imageTarget.gameObject.SetActive(true);
    }


    //private static Vector3 Opencv2DposTo3Dpos(int x, int y, int textureWidth, int textureHeight, Camera cam)
    //{
    //    var px = x / (float)textureWidth;
    //    var py = y / (float)textureHeight;

    //}
}