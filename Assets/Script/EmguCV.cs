using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace upc
{
    // EmguCV 관련 추가 helper 들

    public static class EmguCV
    {
        public static Mat CreateMat(IEnumerable<Vector3> from, bool fixRowSize = true)
        {
            var count = from.Count();
            var src = new float[count * 3];

            var i = 0;
            foreach (var f in from)
            {
                src[i++] = f.x;
                src[i++] = f.y;
                src[i++] = f.z;
            }

            var mat = new Mat(count, 3, DepthType.Cv32F, 1);
            Marshal.Copy(src, 0, mat.DataPointer, src.Length);
            if (fixRowSize) return mat.T();
            return mat;
        }

        public static Image<Bgr, byte> CreateCvImage(this Texture2D tex)
        {
            var tmpFilePath = Application.temporaryCachePath + "/stupid.png";
            var png = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(tmpFilePath, png);
            var img = new Image<Bgr, byte>(tmpFilePath);
            return img;
        }

        public static Texture2D CreateTexture(this Image<Bgr, byte> img)
        {
            var tmpFilePath = Application.temporaryCachePath + "/stupid.png";
            img.Save(tmpFilePath);
            var tex = new Texture2D(img.Width, img.Height);
            tex.LoadImage(System.IO.File.ReadAllBytes(tmpFilePath));
            return tex;
        }



        public static (DepthType type, int channels) GetTypeAndChannel(Texture2D tex)
        {
            switch(tex.format)
            {
                case TextureFormat.Alpha8: return (DepthType.Cv8U, 1);
            }
            throw new System.NotSupportedException($"not supported data format : {tex.format}" );
        }


        public static Mat CreateMat(float[] from, bool fixRowSize = true)
        {
            var mat = new Mat(from.Length, 1, DepthType.Cv32F, 1);
            Marshal.Copy(from, 0, mat.DataPointer, from.Length);
            if (fixRowSize) return mat.T();
            return mat;
        }

        public static (Mat W, Mat U, Mat V) ComputeSvd(this Mat mat, SvdFlag flag = SvdFlag.Default)
        {
            var svdW = new Mat();
            var svdU = new Mat();
            var svdV = new Mat();
            CvInvoke.SVDecomp(mat, svdW, svdU, svdV, flag);
            return (svdW, svdU, svdV);
        }

        public static (double variance, Matrix<int> labels, Mat centers) Kmeans(this Mat samples, int clusterCount, MCvTermCriteria criteria, int attempts = 5, KMeansInitType flag = KMeansInitType.PPCenters)
        { // https://deep-learning-study.tistory.com/292
            var outBestLabels = new Matrix<int>(samples.Rows, 1); // label 은 int 여야 한다.
            var outCenters = new Mat();
            var variance = CvInvoke.Kmeans(samples, clusterCount, outBestLabels, criteria, attempts, flag, outCenters);

            return (variance, outBestLabels, outCenters);
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

        public static Vector3 GetRowVector3(this Mat mat, int col)
        {
            Debug.Assert(mat.Rows == 3);
            return new Vector3
                (mat.GetFloatValue(col, 0)
                , mat.GetFloatValue(col, 1)
                , mat.GetFloatValue(col, 2));
        }

        public static Vector3 GetVector3(this Mat mat, int row)
        {
            Debug.Assert(mat.Cols == 3);
            var value = new float[3];
            Marshal.Copy(mat.DataPointer + (row * mat.Cols) * mat.ElementSize, value, 0, value.Length);
            return new Vector3(value[0], value[1], value[2]);
        }

        public static void SetRowVector3(this Mat mat, int col, Vector3 val)
        {
            Debug.Assert(mat.Rows == 3);
            SetFloatValue(mat, col, 0, val[0]);
            SetFloatValue(mat, col, 1, val[1]);
            SetFloatValue(mat, col, 2, val[2]);
        }

        public static void SetVector3(this Mat mat, int row, Vector3 val)
        {
            Debug.Assert(mat.Cols == 3);
            var target = new float[] { val.x, val.y, val.z };
            Marshal.Copy(target, 0, mat.DataPointer + (row * mat.Cols) * mat.ElementSize, target.Length);
        }
    }
}