using UnityEngine;

namespace upc.Component
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PointCloudRenderer
        : MonoBehaviour
    {
        public Shader pointCloudShader;
        public Color[] Colors { get; private set; }

        public void ApplyColors()
        {
            mf.mesh.colors = Colors;
        }

        private MeshFilter mf;
        private MeshRenderer mr;

        private void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
        }

        public void Setup(PointCloud src)
        {
            mf.mesh.vertices = src.Points;
            mf.mesh.normals = src.Normals;

            var indecies = new int[src.Count];
            for (var i = 0; i < indecies.Length; ++i) indecies[i] = i;
            mf.mesh.SetIndices(indecies, MeshTopology.Points, 0); // as point

            // colors
            Colors = new Color[src.Count];

            // renderer setup
            if (!pointCloudShader) pointCloudShader = Shader.Find("Particles/Standard Unlit");
            mr.material = new Material(pointCloudShader);
        }
    }
}