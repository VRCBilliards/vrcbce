
using UdonSharp;
using UnityEngine;

namespace FairlySadPanda.UsefulThings
{
    [AddComponentMenu("FSP/Utilities/Interact Toggle")]
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
