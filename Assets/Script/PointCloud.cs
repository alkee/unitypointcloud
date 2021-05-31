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

        public Dictionary<string, float[]> ScalarValues { get; private set; } = new Dictionary<string, float[]>();
        private readonly Octree<int> octree;

        public PointCloud(Mesh src)
        {
            Points = src.vertices;
            Count = Points.Length;
            Normals = src.normals;
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
                , diagonalLength / 100); // cloud comapre ÀÇ ±âº» geometry radius; https://bitbucket.org/alkee_skia/mars3/issues/230/scene-idea#comment-60583560
            for (var i = 0; i < Count; ++i)
            {
                octree.Add(i, Points[i]);
            }
        }

        public List<int/*point indexes*/> GetPointIndecies(Vector3 center, float radius)
        {
            return octree.GetNearBy(center, radius);
        }

        public List<Vector3> GetPoints(List<int> indecies)
        {
            if (indecies == null) throw new ArgumentNullException(nameof(indecies));
            return indecies.Select(x => Points[x]).ToList();
        }

        public List<Vector3> GetPoints(Vector3 center, float radius)
        {
            var indecies = GetPointIndecies(center, radius);
            return GetPoints(indecies);
        }
    }
}