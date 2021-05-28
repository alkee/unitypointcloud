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
        pcr.Setup();
    }
}
