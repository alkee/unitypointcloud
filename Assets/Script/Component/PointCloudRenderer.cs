using UnityEngine;

namespace upc.Component
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PointCloudRenderer : MonoBehaviour
    {
        public WavefrontObjMesh obj;
        public Shader pointCloudShader;
        public bool drawOctree;

        private MeshFilter mf;
        private MeshRenderer mr;

        private Mesh target;
        private Octree<int/*vertex index*/> octree;

        private void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            if (!pointCloudShader) pointCloudShader = Shader.Find("Particles/Standard Unlit");

            Debug.Assert(obj);
        }

        public void Setup()
        {
            using (new ElapsedTimeLogger("PointCloudSetup"))
            {
                mr.material = new Material(pointCloudShader);

                target = obj.GetComponentInChildren<MeshFilter>().mesh;
                mf.mesh.vertices = target.vertices;
                mf.mesh.normals = target.normals;
                mf.mesh.colors = target.colors;

                octree = new Octree<int>(mf.mesh.bounds.size.magnitude, mf.mesh.bounds.center, 0.002f);
                var indecies = new int[target.vertexCount];
                for (var i = 0; i < indecies.Length; ++i)
                {
                    indecies[i] = i;
                }
                mf.mesh.SetIndices(indecies, MeshTopology.Points, 0);
            }

            var vertices = target.vertices;
            var pointIndexcies = mf.mesh.GetIndices(0);

            using (new ElapsedTimeLogger("octree insersion"))
            {
                for(var i=0;i<vertices.Length;++i)
                {
                    octree.Add(pointIndexcies[i], vertices[i]);
                }
                //octree.Add(mf.mesh.GetIndices(0), target.vertices);
            }

            Debug.Log($"octree {octree.Count} / vertices {target.vertexCount}");
        }

        private void OnDrawGizmos()
        {
            if (octree == null || drawOctree == false) return;

            octree.DrawAllBounds();
            octree.DrawAllObjects();
        }

        public Color[] Colors
        {
            get { return mf.mesh.colors; }
            set { mf.mesh.colors = value; }
        }

        public Vector3[] Vertices
        {
            get { return mf.mesh.vertices; }
        }

        public Vector3[] Normals
        {
            get { return mf.mesh.normals; }
        }
    }
}