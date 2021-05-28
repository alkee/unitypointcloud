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


        public void Clear()
        {
            foreach (Transform t in transform) Destroy(t.gameObject);
        }

        public void Load()
        {
            Debug.Assert(string.IsNullOrEmpty(sourceFilePath) == false);
            Load(sourceFilePath, lhsSourceCoordination);
        }

        public void Load(string sourceFilePath, bool lhsSourceCoordination = true)
        {
            Clear();

            var objFile = ObjFile.FromFile(sourceFilePath);

            // build material
            var defaultMaterial = new Material(diffuseShader);
            var materials = new Dictionary<string, Material> { { "default", defaultMaterial } };
            // TODO: objFile.MaterialLibraries support

            var lhs = lhsSourceCoordination ? -1 : 1;
            var vertices = objFile.Vertices.Select(v => new Vector3(v.Position.X * lhs, v.Position.Y, v.Position.Z)).ToArray();
            var normals = objFile.VertexNormals?.Select(n => new Vector3(n.X * lhs, n.Y, n.Z)).ToArray();
            // TODO: vertex color support

            var group = objFile.DefaultGroup;
            if (group != null)
            {
                var obj = CreateMesh(vertices, normals, group, defaultMaterial, transform, lhsSourceCoordination);
            }

            // TODO: group mesh 지원
        }

        private static int[] CreateVertexIndecies(in Vector3[] vertices, ObjGroup group, bool flipFace)
        {
            var triangleIndecies = new List<int>();
            foreach (var f in group.Faces)
            {
                // 기준점
                triangleIndecies.Add(f.Vertices[0].Vertex - 1); // .obj 내에서는 index 가 1 부터시작

                // face 의 flipping 은 face index 순서를 바꾸는 것. : https://youtu.be/eJEpeUH1EMg?t=196
                int step = flipFace ? -1 : 1;
                int index = flipFace ? f.Vertices.Count - 1 : 1;
                while (index > 0 && index < f.Vertices.Count)
                {
                    triangleIndecies.Add(f.Vertices[index].Vertex - 1); // .obj 내에서는 index 가 1 부터시작
                    index += step;
                }
                // TODO: uv (texture) 지원
            }
            return triangleIndecies.ToArray();
        }

        private static GameObject CreateMesh(in Vector3[] vertices, in Vector3[] normals, ObjGroup group, Material material, Transform parent, bool flipFace)
        {
            var obj = new GameObject(group.Name ?? "unnamed");

            // mesh filter setup
            var mf = obj.AddComponent<MeshFilter>();
            var mesh = mf.mesh;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = CreateVertexIndecies(vertices, group, flipFace);

            // mesh renderer setup
            var mr = obj.AddComponent<MeshRenderer>();
            mr.material = material;

            // game object setup
            obj.transform.parent = parent;
            obj.transform.localPosition = Vector3.zero;

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