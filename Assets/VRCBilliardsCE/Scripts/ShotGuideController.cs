using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliards
{
    [RequireComponent(typeof(LineRenderer))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShotGuideController : UdonSharpBehaviour
    {
        public LayerMask tableLayers;
        public float maxLengthOfLine;

        private LineRenderer line;
        private RaycastHit hit;
        private Vector3 defaultEndOfLine;

        public void Start()
        {
            line = GetComponent<LineRenderer>();
            defaultEndOfLine = new Vector3(maxLengthOfLine, 0, 0);
        }

        public void Update()
        {
            if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.right), out hit, maxLengthOfLine, tableLayers))
            {

                line.SetPosition(1, new Vector3(hit.distance, 0, 0));
                line.endWidth = Mathf.Lerp(line.startWidth, 0, Mathf.InverseLerp(0, maxLengthOfLine, hit.distance));
            }
            else
            {
                line.SetPosition(1, defaultEndOfLine);
                line.endWidth = 0;
            }
        }
    }
}