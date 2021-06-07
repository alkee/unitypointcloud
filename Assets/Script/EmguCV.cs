using Emgu.CV;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace upc
{
    // EmguCV 관련 추가 helper 들

    public static class EmguCV
    {
        public static Mat ComputeSvdU(this Mat mat, Emgu.CV.CvEnum.SvdFlag flag = Emgu.CV.CvEnum.SvdFlag.Default)
        {
            var svdW = new Mat();
            var svdU = new Mat();
            var svdV = new Mat();
            CvInvoke.SVDecomp(mat, svdW, svdU, svdV, flag);
            return svdU;
        }

        #region Set/Get float element

        // ref https://stackoverflow.com/a/32559496
        public static float GetFloatValue(this Mat mat, int col, int row)
        {
            var value = new float[1];
            Marshal.Copy(mat.DataPointer + (row * mat.Cols + col) * mat.ElementSize, value, 0, 1);
            return value[0];
        }

        public static void SetFloatValue(this Mat mat, int col, int row, float value)
        {
            var target = new float[] { value };
            Marshal.Copy(target, 0, mat.DataPointer + (row * mat.Cols + col) * mat.ElementSize, 1);
        }

        #endregion Set/Get float element

        // col 위치에 각 row 방향으로.. (row 셋팅하고 Transpose 하는 게 효율적이긴 할텐데..)

        public static Vector3 GetVector3(this Mat mat, int col)
        {
            return new Vector3
                (mat.GetFloatValue(col, 0)
                , mat.GetFloatValue(col, 1)
                , mat.GetFloatValue(col, 2));
        }

        public static void SetVector3(this Mat mat, int col, Vector3 val)
        {
            SetFloatValue(mat, col, 0, val[0]);
            SetFloatValue(mat, col, 1, val[1]);
            SetFloatValue(mat, col, 2, val[2]);
        }

        public static void SetVector3Values(this Mat mat, IEnumerable<Vector3> values)
        {
            var i = 0;
            foreach (var val in values) mat.SetVector3(i++, val);
        }
    }
}