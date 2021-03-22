
using UdonSharp;
using UnityEngine;

namespace VRCBilliards
{
    public class PoolSetHeight : UdonSharpBehaviour
    {
        public GameObject heightCalibrator;
        public Material mat;

        public void Start()
        {
            mat.SetFloat("_ShadowOffset", heightCalibrator.transform.position.y);
        }
    }
}