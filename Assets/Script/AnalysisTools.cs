using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace upc
{
    public class AnalysisTools
    {
        public static Plane? GetBestFittingPlane(IEnumerable<Vector3> points)
        {
            var count = points.Count();
            if (count < 3) return null;
            var center = points.GetMeanVector();
            var centered = points.Select(x => x - center);
            var mat = EmguCV.CreateMat(centered);
            var svdU = mat.ComputeSvd().U;
            var normal = svdU.GetRowVector3(2);
            return new Plane(normal, center);
        }

        public static float Roughness(Vector3 point, IEnumerable<Vector3> points)
        {
            var count = points.Count();
            if (count == 3) return 0;
            var plane = GetBestFittingPlane(points);
            if (plane.HasValue == false) return float.NaN;
            return Mathf.Abs(plane.Value.GetDistanceToPoint(point));
        }

        public static float NormalChangeRate(IEnumerable<Vector3> points)
        { // ref : https://github.com/CloudCompare/CCCoreLib/blob/be52fc2f9981a80cd457cd914f44f17f6ebf04f1/src/Neighbourhood.cpp#L980-L1018
            // cloudcompare - Surface variation(L3 / (L1 + L2 + L3)) 과 값이 같다 (L3 는 eigen value 최소값이므로)

            var count = points.Count();
            if (count == 3) return 0;
            if (count < 3) return float.NaN;

            var center = points.GetMeanVector();
            var centered = points.Select(x => x - center);
            var mat = EmguCV.CreateMat(centered);

            var (W, U, _) = mat.ComputeSvd();

            var e1 = W.GetFloatValue(0, 0);
            var eigenValue1 = e1 * e1;
            var e2 = W.GetFloatValue(0, 1);
            var eigenValue2 = e2 * e2;
            var e3 = W.GetFloatValue(0, 2);
            var eigenValue3 = e3 * e3;

            var sum = eigenValue1 + eigenValue2 + eigenValue3;
            var min = Mathf.Min(eigenValue1, eigenValue2, eigenValue3);
            return min / sum;
        }

        public static (float u, float v) EffectiveEnergy(Vector3 point, Vector3 normal, IEnumerable<Vector3> points)
        { // https://www.sciencedirect.com/science/article/abs/pii/S0167865506000663?casa_token=6ec1yWFMLP0AAAAA:pSQjaDcgrO1M3tHA39DSzWCMcQqBga-4cry9liWmbzlb7OOKEQllWfl-xgVt2SKrp-ohsE9Y6Q
            var count = points.Count();

            //var center = points.GetMeanVector();
            var sum = 0.0f;
            foreach (var p in points)
            {
                var d = Vector3.Dot((point - p), normal);
                sum += d;
            }

            // mean
            var u = sum / count;

            // variance
            var vsum = 0.0f;
            foreach (var p in points)
            {
                var d = Vector3.Dot((point - p), normal);
                var s = (d - u) * (d - u);
                vsum += s;
            }
            var v = vsum / count;

            return (u, v);
        }
    }
}