using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.Udon;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// A utility script to help with activating things when Interacting with some button or somesuch in worlds.
    /// Includes handling if the activation should be networked. Easy networking! Joy.
    /// </summary>
    [AddComponentMenu("VRCBCE/Utilities/Activate Udon Event")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ActivaterUdonEvent : UdonSharpBehaviour
    {
        [Tooltip("Do we want to trigger the UdonBehaviour we're triggering for other people in the instance?")]
        public bool networked;
        [Tooltip("Do we want to trigger the UdonBehaviour we're triggering for everyone in the instance, or just the owner of the behaviour we're triggering?")]
        public bool networkedAll;

        [Header("Trigger a Udon behaviour doing something")]
        public UdonBehaviour behaviour;
        public string eventName;

        [Header("Trigger an animator doing something")]
        public Animator animator;
        
        [Tooltip("Do we trigger this animation via a bool or a trigger?")]
        public bool isTriggeredViaBool;
        [Tooltip("What's the name of the bool or trigger?")]
        public string stateName;
        [Tooltip("Should we set the value to true or false? This isn't synced if you change it locally!")]
        public bool boolStateValue;
        
        [Header("Trigger a sound when interacted with, potentially for everyone if networked is set to TRUE")]
        public AudioSource audioSource;
        
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
            
            if (audioSource != null)
            {
                if (networked)
                {
                    SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(PlayAudio));
                }
                else
                {
                    audioSource.Play();
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
        
        public void PlayAudio()
        {
            if (audioSource.clip != null)
            {
                audioSource.Play();
            }
        }
    }

}