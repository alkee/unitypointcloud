using UnityEngine;

namespace upc.Component
{
    [RequireComponent(typeof(MeshFilter))]
    public class PointCloudRenderer : MonoBehaviour
    {
        public WavefrontObjMesh obj;
        public Shader pointCloudShader;

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
                if (obj.lhsSourceCoordination) { transform.localScale = new Vector3(-1, 1, 1); }
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
                    octree.Add(i, Vertices[i]);
                }
                mf.mesh.SetIndices(indecies, MeshTopology.Points, 0);
            }

            Debug.Log($"octree {octree.Count} positions");
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