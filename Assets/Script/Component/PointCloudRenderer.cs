using UnityEngine;

namespace upc.Component
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PointCloudRenderer : MonoBehaviour
    {
        public Shader pointCloudShader;
        public bool drawOctree;

        private MeshFilter mf;
        private MeshRenderer mr;
        private Octree<int/*vertex index*/> octree;

        private Vector3[] points; // mesh.vertices 를 사용하면 항상 복사가 발생하기 때문에 source 배열을 이용해 vertex 정보에 접근해야할 것. ; https://github.com/alkee/unitypointcloud/issues/1#issuecomment-850802513
        private Vector3[] normals;

        private void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
        }

        public void Setup(Mesh source)
        {
            points = source.vertices;
            normals = source.normals;
            // TODO: 적당한 minimal node size 자동찾기, 혹은 parameter 이용
            octree = new Octree<int>(source.bounds.size.magnitude, source.bounds.center, 0.002f);

            using (new ElapsedTimeLogger("PointCloudSetup"))
            {
                mf.mesh.vertices = points;
                mf.mesh.normals = normals;
                
                var indecies = new int[points.Length];
                for (var i = 0; i < indecies.Length; ++i)
                {
                    indecies[i] = i;
                    octree.Add(i, points[i]);
                }
                mf.mesh.SetIndices(indecies, MeshTopology.Points, 0);
            }

            // renderer setup
            if (!pointCloudShader) pointCloudShader = Shader.Find("Particles/Standard Unlit");
            mr.material = new Material(pointCloudShader);
        }

        private void OnDrawGizmos()
        {
            // octree drawing
            if (octree !=null && drawOctree)
            {
                octree.DrawAllBounds();
                octree.DrawAllObjects();
            }
        }
    }
}