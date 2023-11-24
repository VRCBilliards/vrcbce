using UdonSharp;
using UnityEngine;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// A basic ol' interact toggle script, that's nicer than Merlin's because Merlin forgot to do a null check :>
    /// </summary>
    [AddComponentMenu("FSP/Utilities/Interact Toggle")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InteractToggle : UdonSharpBehaviour
    {
        [Tooltip("List of objects to toggle on and off")]
        public GameObject[] toggleObjects;

        public override void Interact()
        {
            foreach (GameObject toggleObject in toggleObjects)
            {
                if (toggleObject)
                {
                    toggleObject.SetActive(!toggleObject.activeSelf);
                }
            }
        }
    }
}
