using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace upc.Component
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PointCloudRenderer : MonoBehaviour
    {
        public Shader pointCloudShader;
        public bool drawOctree;

        public Vector3[] Points { get; private set; } // mesh.vertices 를 사용하면 항상 복사가 발생하기 때문에 source 배열을 이용해 vertex 정보에 접근해야할 것. ; https://github.com/alkee/unitypointcloud/issues/1#issuecomment-850802513
        public Vector3[] Normals { get; private set; }
        public Color[] Colors { get; private set; }

        public void ApplyColors()
        {
            mf.mesh.colors = Colors;
        }

        public List<int/*point indexes*/> GetPointIndecies(Vector3 center, float radius)
        {
            if (octree == null) return null; // not initialized yet
            return octree.GetNearBy(center, radius);
        }

        public List<Vector3> GetPoints(List<int> indecies)
        {
            if (indecies == null) return null;
            return indecies.Select(x => Points[x]).ToList();
        }

        public List<Vector3> GetPoints(Vector3 center, float radius)
        {
            var indecies = GetPointIndecies(center, radius);
            return GetPoints(indecies);
        }

        private MeshFilter mf;
        private MeshRenderer mr;
        private Octree<int/*vertex index*/> octree;

        private void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
        }

        public void Setup(Mesh source)
        {
            Points = source.vertices;
            Normals = source.normals;
            // TODO: 적당한 minimal node size 자동찾기, 혹은 parameter 이용
            octree = new Octree<int>(source.bounds.size.magnitude, source.bounds.center, 0.002f);

            using (new ElapsedTimeLogger("PointCloudSetup"))
            {
                mf.mesh.vertices = Points;
                mf.mesh.normals = Normals;

                var indecies = new int[Points.Length];
                for (var i = 0; i < indecies.Length; ++i)
                {
                    indecies[i] = i;
                    octree.Add(i, Points[i]);
                }
                mf.mesh.SetIndices(indecies, MeshTopology.Points, 0);
                Colors = mf.mesh.colors;
                if (Colors.Length != Points.Length) Colors = new Color[Points.Length];
            }

            Debug.Log($"{Points.Length} points of cloud");

            // renderer setup
            if (!pointCloudShader) pointCloudShader = Shader.Find("Particles/Standard Unlit");
            mr.material = new Material(pointCloudShader);
        }

        private void OnDrawGizmos()
        {
            // octree drawing
            if (octree != null && drawOctree)
            {
                octree.DrawAllBounds();
                octree.DrawAllObjects();
            }
        }
    }
}