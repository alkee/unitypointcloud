using UnityEngine;

namespace upc.Component
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PointCloudRenderer
        : MonoBehaviour
    {
        public Shader pointCloudShader;
        public Color[] Colors { get; private set; }

        [Header("Debug - Gizmo drawing")]
        [Tooltip("scene 에서 선택된 경우 octree 구조를 editor 에서 보여줌")]
        public bool drawOctreeBounds;

        private PointCloud src;

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

        private void OnDrawGizmosSelected()
        {
            if (src == null) return;
            if (drawOctreeBounds) src.DrawOctreeBounds();
        }

        public void Setup(PointCloud src)
        {
            this.src = src;

            mf.mesh.vertices = src.Points;
            mf.mesh.normals = src.Normals;

            var indices = new int[src.Count];
            for (var i = 0; i < indices.Length; ++i) indices[i] = i;
            mf.mesh.SetIndices(indices, MeshTopology.Points, 0); // as point

            // colors
            Colors = new Color[src.Count];

            // renderer setup
            if (!pointCloudShader) pointCloudShader = Shader.Find("Particles/Standard Unlit");
            mr.material = new Material(pointCloudShader);
        }
    }
}