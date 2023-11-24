
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCBilliardsCE.Packages.com.vrcbilliards.vrcbce.Runtime.Scripts
{
    /// <summary>
    /// Handles some logic in the colour picker.
    /// </summary>
    
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PrefabColor : UdonSharpBehaviour
    {
        private ColorPicker playerPanel;
        [FormerlySerializedAs("PrefabedColor")] public Color prefabColour = new Color(1.00f, 1.00f, 1.00f, 1.00f);

        // Capped at 3 as it gets blinding depending shaders and world post processing setup
        [Range(0, 3)] public float intensity = 1;

        public string materialName = "_Color";

        //public Renderer ButtonColor;
        private Image button;

        private void Start()
        {
            playerPanel = GetComponentInParent<ColorPicker>();
            button = GetComponent<Image>();
            _ButtonColors(false);
        }

        public void _ButtonPress()
        {
            float hue, sat, brightness;
            Color.RGBToHSV(prefabColour, out hue, out sat, out brightness);
            playerPanel._PrefabPicker(hue, sat, brightness, intensity);
        }

        public void _ButtonColors(bool IO)
        {
            if (IO)
            {
                button.color = prefabColour;
            }
            else
            {
                button.color = prefabColour * Color.black;
            }
        }
    }
}