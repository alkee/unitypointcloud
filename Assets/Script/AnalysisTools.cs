using Emgu.CV;
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
            var mat = new Emgu.CV.Mat(3, points.Count(), Emgu.CV.CvEnum.DepthType.Cv32F, 1);
            var centered = points.Select(x => x - center);
            mat.SetVector3Values(centered);
            var svdU = mat.ComputeSvdU();
            var normal = svdU.GetVector3(2);
            return new Plane(normal, center);
        }

        public static float Roughness(Vector3 point, IEnumerable<Vector3> group)
        {
            var count = group.Count();
            if (count == 3) return 0;
            var plane = GetBestFittingPlane(group);
            if (plane.HasValue == false) return float.NaN;
            return Mathf.Abs(plane.Value.GetDistanceToPoint(point));
        }

        public static float NormalChangeRate(Vector3 point, IEnumerable<Vector3> group)
        { // ref : https://github.com/CloudCompare/CCCoreLib/blob/be52fc2f9981a80cd457cd914f44f17f6ebf04f1/src/Neighbourhood.cpp#L980-L1018
            var count = group.Count();
            if (count == 3) return 0;
            if (count < 3) return float.NaN;

            var mat = new Mat(3, count, Emgu.CV.CvEnum.DepthType.Cv32F, 1);
            var center = group.GetMeanVector();
            var centered = group.Select(x => x - center);
            mat.SetVector3Values(centered);

            var svdW = new Mat();
            var svdU = new Mat();
            var svdV = new Mat();
            CvInvoke.SVDecomp(mat, svdW, svdU, svdV, Emgu.CV.CvEnum.SvdFlag.Default);

            var e1 = svdW.GetFloatValue(0, 0);
            var eigenValue1 = e1 * e1;
            var e2 = svdW.GetFloatValue(0, 1);
            var eigenValue2 = e2 * e2;
            var e3 = svdW.GetFloatValue(0, 2);
            var eigenValue3 = e3 * e3;

            var sum = eigenValue1 + eigenValue2 + eigenValue3;
            var min = Mathf.Min(eigenValue1, eigenValue2, eigenValue3);
            return min / sum;
        }
    }
}