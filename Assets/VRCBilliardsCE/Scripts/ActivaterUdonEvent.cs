using UdonSharp;
using UnityEngine;
using VRC.Udon;

namespace FairlySadPanda.UsefulThings
{
    [AddComponentMenu("FSP/Utilities/Activate Udon Event")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ActivaterUdonEvent : UdonSharpBehaviour
    {
        public bool networked;
        public bool networkedAll;

        [Header("Trigger a Udon behaviour doing something")]
        public UdonBehaviour behaviour;
        public string eventName;

        [Header("Trigger an animator doing something")]
        public Animator animator;

        public bool isTriggeredViaBool;
        public string stateName;
        public bool boolStateValue;

        public override void Interact()
        {
            if (behaviour != null)
            {
                Debug.Log($"{gameObject.name}: {behaviour.gameObject.name}: {eventName}");

                if (networked)
                {
                    if (networkedAll)
                    {
                        behaviour.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, eventName);
                    }
                    else
                    {
                        behaviour.SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, eventName);
                    }
                }
                else
                {
                    behaviour.SendCustomEvent(eventName);
                }
            }

            if (animator != null)
            {
                if (networked)
                {
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(ActivateAnimation));
                }
                else
                {
                    animator.SetBool(stateName, boolStateValue);
                }
            }
        }

        public void ActivateAnimation()
        {
            Debug.Log("ActivateAnimation");

            if (isTriggeredViaBool)
            {
                animator.SetBool(stateName, boolStateValue);
            }
            else
            {
                Debug.Log("Honk");
                animator.SetTrigger(stateName);
            }
        }

        public void Reset()
        {
            if (animator != null)
            {
                animator.SetBool(stateName, false);
            }
        }
    }
}