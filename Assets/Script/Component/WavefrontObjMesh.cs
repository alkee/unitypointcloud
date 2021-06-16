using JeremyAnsel.Media.WavefrontObj; // https://github.com/JeremyAnsel/JeremyAnsel.Media.WavefrontObj
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace upc.Component
{
    public class WavefrontObjMesh
        : MonoBehaviour
    {
        [Header("Initial load settings")]
        public string sourceFilePath;

        public bool lhsSourceCoordination = true;

        [Header("Defaults")]
        public Shader diffuseShader;

        public MeshFilter DefaultMeshFilter { get; private set; }

        public void Clear()
        {
            foreach (Transform t in transform) Destroy(t.gameObject);
            DefaultMeshFilter = null;
        }

        public void Load()
        {
            Debug.Assert(string.IsNullOrEmpty(sourceFilePath) == false);
            Load(sourceFilePath, lhsSourceCoordination);
        }

        public GameObject Load(string sourceFilePath, bool lhsSourceCoordination = true)
        {
            Clear();

            var objFile = ObjFile.FromFile(sourceFilePath);

            // build material
            var defaultMaterial = new Material(diffuseShader);
            //var materials = new Dictionary<string, Material> { { "default", defaultMaterial } };
            // TODO: objFile.MaterialLibraries support

            // TODO: group mesh/sub mesh 지원
            var gameObj = CreateMeshObject(objFile, defaultMaterial, transform, lhsSourceCoordination);
            DefaultMeshFilter = gameObj.GetComponent<MeshFilter>();
            return gameObj;
        }

        private static GameObject CreateMeshObject(ObjFile source, Material defaultMaterial, Transform parent, bool lhsSourceCoordination)
        {
            // prepare data
            var lhs = lhsSourceCoordination ? -1 : 1;
            var vertices = source.Vertices.Select(v => new Vector3(v.Position.X * lhs, v.Position.Y, v.Position.Z)).ToArray();
            var normals = source.VertexNormals?.Select(n => new Vector3(n.X * lhs, n.Y, n.Z)).ToArray();
            var faces = new List<int>();
            foreach (var f in source.Faces)
            {
                // face 의 flipping 은 face index 순서를 바꾸는 것. : https://youtu.be/eJEpeUH1EMg?t=196
                for (var i = lhsSourceCoordination ? 2 : 0; i >= 0 && i < 3; i += lhs)
                    faces.Add(f.Vertices[i].Vertex - 1); // wavefront .obj 의 index 는 1 부터 시작.
            }

            var obj = new GameObject("obj mesh");
            obj.transform.parent = parent;

            // mesh filter setup
            var mf = obj.AddComponent<MeshFilter>();
            var mesh = mf.mesh;
            mesh.vertices = vertices;
            mesh.triangles = faces.ToArray();
            if (normals.Length == vertices.Length)
            {
                mesh.normals = normals;
            }
            else
            {
                Debug.LogWarning($"normal count dismatched. vertices : {vertices.Length}, normals : {normals.Length}. recalculating ...");
                mesh.RecalculateNormals();
            }
            Debug.Log($"mesh created at {mesh.bounds:F4}, vertices: {vertices.Length}, faces: {faces.Count / 3}");

            // mesh renderer setup
            var mr = obj.AddComponent<MeshRenderer>();
            mr.material = defaultMaterial;

            return obj;
        }

        #region Unity message handlers

        private void Awake()
        {
            if (!diffuseShader)
            {
                diffuseShader = Shader.Find("Standard"); // initializer 에서 Shader.Find 를 사용할 수 없다.
                Debug.Assert(diffuseShader);
            }
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(sourceFilePath)) return;
            Load(sourceFilePath);
        }

        #endregion Unity message handlers
    }
}