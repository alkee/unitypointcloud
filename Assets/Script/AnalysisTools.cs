using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace upc
{
    public class AnalysisTools
    {
        public static Plane GetBestFittingPlane(IEnumerable<Vector3> points)
        {
            var center = points.GetMeanVector();
            var mat = new Emgu.CV.Mat(3, points.Count(), Emgu.CV.CvEnum.DepthType.Cv32F, 1);
            var centered = points.Select(x => x - center);
            mat.SetVector3Values(centered);
            var svdU = mat.ComputeSvdU();
            var normal = svdU.GetVector3(2);
            return new Plane(normal, center);
        }
    }
}