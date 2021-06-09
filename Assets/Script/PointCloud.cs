using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace upc
{
    public class PointCloud
    {
        public int Count { get; private set; }
        public Vector3[] Points { get; private set; }
        public Vector3[] Normals { get; private set; }
        public Bounds Bounds { get; private set; }
        public int[] Faces { get; private set; }

        private readonly Octree<int> octree;

        public PointCloud(Mesh src)
        {
            Points = src.vertices;
            Count = Points.Length;
            Normals = src.normals;
            Faces = src.triangles;
            Bounds = src.bounds;
            if (Normals == null || Normals.Length != Count)
            {
                // TODO: automatic normal calculation
                Debug.LogWarning("Pointcloud has invalid normals");
                Normals = new Vector3[Points.Length];
            }

            // octree calculation
            var diagonalLength = (Bounds.max - Bounds.min).magnitude;
            octree = new Octree<int>(diagonalLength, Bounds.center
                , diagonalLength / 100); // cloud comapre 의 기본 geometry radius; https://bitbucket.org/alkee_skia/mars3/issues/230/scene-idea#comment-60583560
            for (var i = 0; i < Count; ++i)
            {
                octree.Add(i, Points[i]);
            }
        }

        public List<int/*point indexes*/> GetPointIndices(Vector3 center, float radius)
        {
            return octree.GetPointIndicesIn(center, radius);
        }

        public List<Vector3> GetPoints(List<int> indices)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            return indices.Select(x => Points[x]).ToList();
        }

        public List<Vector3> GetPoints(Vector3 center, float radius)
        {
            var indices = GetPointIndices(center, radius);
            return GetPoints(indices);
        }

        // 이 함수는 OnGizmoDraw 함수에서만 사용가능
        public void DrawOctreeBounds()
        {
            if (octree == null) return;
            octree.DrawAllBounds();
        }
    }

    public class ScalarValues
    {
        public float[] Values { get; private set; }
        public float EffectiveCount { get; private set; } // NaN excluded
        public float Min { get; private set; }
        public float Max { get; private set; }
        public float Mean { get; private set; }
        public float Variance { get; private set; }

        public ScalarValues(float[] src)
        {
            if (src is null) throw new ArgumentNullException(nameof(src));
            if (src.Length == 0) throw new ArgumentException("0 size of array", nameof(src));

            Values = src;
            Min = float.MaxValue;
            Max = float.MinValue;
            var sum = 0.0f;
            var nanCount = 0;
            for (var i = 0; i < src.Length; ++i)
            {
                var val = src[i];
                if (float.IsNaN(val)) { ++nanCount; continue; }
                sum += val;
                if (val > Max) Max = val;
                if (val < Min) Min = val;
            }
            if (nanCount == src.Length) throw new ArgumentException($"all NaN array({src.Length})", nameof(src));
            EffectiveCount = src.Length - nanCount;
            Mean = sum / EffectiveCount;

            sum = 0.0f;
            for (var i = 0; i < src.Length; ++i)
            {
                var val = src[i];
                if (float.IsNaN(val)) continue;
                sum += (val - Mean) * (val - Mean);
            }
            Variance = sum / EffectiveCount;
        }
    }
}