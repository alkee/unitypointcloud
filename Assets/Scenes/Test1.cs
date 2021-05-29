using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using upc.Component;

public class Test1 : MonoBehaviour
{
    private WavefrontObjMesh obj;
    private PointCloudRenderer pcr;

    private void Awake()
    {
        obj = FindObjectOfType<WavefrontObjMesh>(true); Debug.Assert(obj);
        pcr = FindObjectOfType<PointCloudRenderer>(true); Debug.Assert(pcr);
    }

    public void Test()
    {
        obj.gameObject.SetActive(false);
        var mesh = obj.GetComponentInChildren<MeshFilter>().mesh; // TODO: 2 개 이상의 submesh 존재하는 경우 처리
        pcr.Setup(mesh);
    }
}
