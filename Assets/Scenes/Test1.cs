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

    [SerializeField]
    private UnityEngine.UI.RawImage imageTarget;

    private void Awake()
    {
        Debug.Assert(lineContainer);

        obj = FindObjectOfType<WavefrontObjMesh>(true); Debug.Assert(obj);
        pcr = FindObjectOfType<PointCloudRenderer>(true); Debug.Assert(pcr);
        if (!imageTarget) imageTarget = FindObjectOfType<UnityEngine.UI.RawImage>(true);
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
    public bool drawEffectiveColor = false;

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
        imageTarget.gameObject.SetActive(false);
        OpenFile();
        CreatePointCloud();
    }

    [Header("Geometric features")]
    public float radius = 0.03f;

    public void Roughness()
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

    public void NormalChangeRate()
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
    { // https://www.sciencedirect.com/science/article/abs/pii/S0167865506000663?via%3Dihub
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
        //Dnn();
        //DetectionFromDepthMap();
        //DetectionFromMeshRendering();
        //FaceLandmark();
        DetectNose();
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

    private System.Drawing.Rectangle[] DetectByCascadeClassifier(string classifierFilename, Emgu.CV.Image<Emgu.CV.Structure.Bgr, byte> img)
    {
        var c = new Emgu.CV.CascadeClassifier(classifierFilename);
        var rs = c.DetectMultiScale(img);
        if (rs.Length == 0) Debug.LogWarning($"CascadeCassifier: no feature detected by {classifierFilename}");

        return rs;
    }

    private System.Drawing.Rectangle[] DetectByCascadeClassifier(string classifierFilename, Texture2D tex)
    {
        var img = EmguCV.CreateCvImage(tex);
        return DetectByCascadeClassifier(classifierFilename, img);
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

        for (var i = 0; i < dimSizes[2]; i++)
        {
            var values = new float[dimSizes[3]];
            System.Runtime.InteropServices.Marshal.Copy(new IntPtr(start.ToInt64() + step * i), values, 0, dimSizes[3]);
            var confident = values[2];
            if (confident < confidenceThreshold) continue;

            float xLeftBottom = values[3] * img.Rows; // Emgu.CV.Test/AutoTestVarious.cs 에서는 rows-cols 이 반대인 듯.
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

    private Texture2D CreateDepthMap(WavefrontObjMesh source, int width, int height, bool bottomToTop = false)
    {
        var tex = new Texture2D(width, height); //, TextureFormat.Alpha8, false);
        var col = obj.GetComponent<MeshCollider>();
        if (!col)
        {
            col = obj.gameObject.AddComponent<MeshCollider>();
        }
        col.sharedMesh = obj.DefaultMeshFilter.mesh;

        var buffer = CreateYDepthBuffer(col, tex.width, tex.height, bottomToTop);

        for (var y = 0; y < tex.height; ++y)
            for (var x = 0; x < tex.width; ++x)
            {
                var val = (float)buffer[x, y] / byte.MaxValue;
                tex.SetPixel(x, y, new Color(val, val, val));
            }

        return tex;
    }

    private static byte[,] CreateYDepthBuffer(MeshCollider col, int width, int height, bool bottomToTop = false)
    {
        var yDir = bottomToTop ? -1 : 1;
        var bbox = col.bounds; // same as col.sharedMesh.bounds ?
        // length per pixel
        var resX = bbox.size.x / (width - 1);
        var resZ = bbox.size.z / (height - 1);

        // prixel per length
        var resY = byte.MaxValue / bbox.size.y;
        var ret = new byte[width, height];

        const int RAY_DISTANCE = 5;
        for (var y = 0; y < height; ++y)
            for (var x = 0; x < width; ++x)
            {
                var x3d = bbox.min.x + resX * x;
                var z3d = bbox.min.z + resZ * y;
                var origin = new Vector3(x3d, bbox.max.y + (1 * yDir)/* out of mesh */, z3d);
                var dir = Vector3.up * (-1 * yDir);
                if (col.Raycast(new Ray(origin, dir), out var hit, RAY_DISTANCE * 2))
                {
                    var val = bottomToTop
                        ? (byte)((bbox.max.y - hit.point.y) * resY)
                        : (byte)((hit.point.y - bbox.min.y) * resY);
                    ret[x, y] = val;
                }
            }
        return ret;
    }

    [Header("Casade Classifier")]
    public string classifierFilename;

    private void DetectionFromDepthMap()
    {
        if (System.IO.File.Exists(classifierFilename) == false)
        {
            Debug.LogWarning($"file not found : {classifierFilename}");
            return;
        }

        obj.gameObject.SetActive(true);
        pcr.gameObject.SetActive(false);

        var tex = CreateDepthMap(obj, 640, 480);
        var rects = DetectByCascadeClassifier(classifierFilename, tex);
        foreach (var r in rects)
        {
            // texture 는 bottom-left 가 (0,0) mat 은 top-left 가 (0,0)
            var rct = new RectInt(r.Left, tex.height - r.Top - r.Height, r.Width, r.Height);
            tex.DrawRect(rct, new Color(0.0f, 1.0f, 0.0f, 0.3f));
        }
        tex.Apply();

        // visualize
        imageTarget.texture = tex;
        imageTarget.gameObject.SetActive(true);
    }

    private void DetectionFromMeshRendering()
    {
        if (System.IO.File.Exists(classifierFilename) == false)
        {
            Debug.LogWarning($"file not found : {classifierFilename}");
            return;
        }

        obj.gameObject.SetActive(true);
        pcr.gameObject.SetActive(false);

        var tex = Camera.main.Render(640, 480, RenderTextureFormat.ARGB32);
        var rects = DetectByCascadeClassifier(classifierFilename, tex);
        foreach (var r in rects)
        {
            // texture 는 bottom-left 가 (0,0) mat 은 top-left 가 (0,0)
            var rct = new RectInt(r.Left, tex.height - r.Top - r.Height, r.Width, r.Height);
            tex.DrawRect(rct, new Color(0.0f, 1.0f, 0.0f, 0.3f));
        }
        tex.Apply();

        // visualize
        imageTarget.texture = tex;
        imageTarget.gameObject.SetActive(true);
    }

    [Header("Face landmark")]
    public string facemarkModel; // https://raw.githubusercontent.com/kurnianggoro/GSOC2017/master/data/lbfmodel.yaml

    private void FaceLandmark()
    {
        // https://docs.opencv.org/3.4.14/d2/d42/tutorial_face_landmark_detection_in_an_image.html
        // https://docs.opencv.org/3.4.14/d7/dec/tutorial_facemark_usage.html
        // https://github.com/emgucv/emgucv/blob/7b824371fd93f37296efa073c56d399f57d178d6/Emgu.CV.Test/AutoTestVarious.cs#L3730

        if (System.IO.File.Exists(classifierFilename) == false)
        {
            Debug.LogWarning($"file not found : {classifierFilename}");
            return;
        }
        if (System.IO.File.Exists(facemarkModel) == false)
        {
            Debug.LogWarning($"file not found : {facemarkModel}");
            return;
        }

        obj.gameObject.SetActive(true);
        pcr.gameObject.SetActive(false);

        var tex = Camera.main.Render(640, 480, RenderTextureFormat.ARGB32);
        var img = EmguCV.CreateCvImage(tex);
        var faceRegions = DetectByCascadeClassifier(classifierFilename, img);
        if (faceRegions.Length == 0) { Debug.LogWarning($"face not detected"); return; }

        using (var facemarkParam = new Emgu.CV.Face.FacemarkLBFParams())
        using (var facemark = new Emgu.CV.Face.FacemarkLBF(facemarkParam))
        using (var vr = new Emgu.CV.Util.VectorOfRect(faceRegions))
        using (var landmarks = new Emgu.CV.Util.VectorOfVectorOfPointF())
        {
            Emgu.CV.Face.FaceInvoke.LoadModel(facemark, facemarkModel); // facemark.LoadModel(facemarkModel);
            Emgu.CV.Face.FaceInvoke.Fit(facemark, img, vr, landmarks); // facemark.Fit(img, vr, landmarks);

            int len = landmarks.Size;
            Debug.Log($"{len} landmark detected");
            for (int i = 0; i < landmarks.Size; i++)
            {
                using (var vpf = landmarks[i])
                {
                    Debug.Log($"  [{i}] has {vpf.Size} points");
                    Emgu.CV.Face.FaceInvoke.DrawFacemarks(img, vpf, new Emgu.CV.Structure.MCvScalar(0, 255, 0));
                    //var points = vpf.ToArray();
                    //foreach (var p in points)
                    //tex.DrawPoint(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y), new Color(0.0f, 1.0f, 0.0f, 0.3f));
                }
            }
        }

        tex = EmguCV.CreateTexture(img);
        //tex.Apply();

        // visualize
        imageTarget.texture = tex;
        imageTarget.gameObject.SetActive(true);
    }

    [Header("Nose detection")]
    public Vector3 faceDirection = Vector3.up;
    [Tooltip("boundingbox 대각선 거리 기준 중심축에서의 휴효거리")]
    [Range(0.01f, 0.5f)]
    public float maximumDistanceRate = 0.15f;
    [Tooltip("평균~최고 사이의 지점")]
    [Range(0.0f, 1.0f)]
    public float thresholdRate = 0.2f;

    private void DetectNose()
    {
        //CalculateEffectiveEnergy();
        NormalChangeRate();

        var bounds = obj.DefaultMeshFilter.mesh.bounds;
        faceDirection.Normalize();
        var centerRay = new Ray(bounds.center, faceDirection);
        var centerPlane = new Plane(faceDirection, bounds.center);

        // CT 의 단면이 나타나는 영역에 effective energy 가 크게 잡히는 경우가 있어 중심에서 먼 점을 제외하기 위함
        var maximumDistance = Vector3.Distance(bounds.min, bounds.max) * maximumDistanceRate;
        var threshold = Mathf.Lerp(sv.Mean, sv.Max, thresholdRate);

        float max = float.MinValue;
        int maxIndex = -1;
        for (var i = 0; i < pc.Points.Length; ++i)
        {
            if (sv.Values[i] < threshold) { if (drawEffectiveColor) sv.Values[i] = sv.Min; continue; }
            var p = pc.Points[i];
            var distanceFromCenterRay = Vector3.Cross(centerRay.direction, p - centerRay.origin).magnitude; // https://answers.unity.com/questions/568773
            if (distanceFromCenterRay > maximumDistance) { if (drawEffectiveColor) sv.Values[i] = float.NaN; continue; } // margin 영역 point 제외

            var distance = centerPlane.GetDistanceToPoint(p);
            if (max < distance) // center 에서 거리(signed)가 최대인 point 찾기
            {
                max = distance;
                maxIndex = i;
            }
        }
        if (maxIndex < 0)
        {
            Debug.LogWarning("nose tip not found");
            return;
        }

        // visualize
        ColorizeScalarField(pcr, sv);

        ClearClusters();
        var noseTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        noseTip.transform.position = pc.Points[maxIndex];
        noseTip.transform.localScale = Vector3.one * 0.01f;
        clusterCenters.Add(noseTip);

    }
}