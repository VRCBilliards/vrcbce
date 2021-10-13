
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC.Udon.Common.Interfaces;
using VRCBilliards;

namespace VRCBilliards
{
    public class colorpicker : UdonSharpBehaviour
    {
        [Header("Interface with the core code")]
        public PoolStateManager PoolTable;
        public bool team1Blue = true;
        private bool isPanelEnabled = false;
        
        [Header("Shader property name and the display material that will be colored")]
        public string ColorName = "_MainTex";
        public Renderer displayOutput;
        
        [Header("HSV values from the Slider and the synced float variables")]
        public Slider sliderHue;
        public Slider sliderSaturation;
        public Slider sliderBrightness;
        [UdonSynced()] public float floatHue = 0;
        [UdonSynced()] public float floatSaturation = 0.75f;
        [UdonSynced()] public float floatBrightness = 1;
        
        [Header("The Colored Buttons that are already set up fro the player")]
        public PrefabColor[] colorButtons;
        [Header("The sound that plays when the buttons are pressed")]
        public AudioSource audioSource;

        private void Start()
        {
            Color Temp = new Color(floatHue, floatSaturation, floatBrightness, 1);
            displayOutput.material.SetColor(ColorName, Temp);
            SetColor();
        }


        public void _SetHue()
        {

            if (isPanelEnabled)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                floatHue = sliderHue.value;
                RequestSerialization();
                SetColor();
            }
        }

        public void _SetSaturation()
        {
            if (isPanelEnabled)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                floatSaturation = sliderSaturation.value;
                RequestSerialization();
                SetColor();
            }
        }

        public void _SetBrightness()
        {
            if (isPanelEnabled)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                floatBrightness = sliderBrightness.value;
                RequestSerialization();
                SetColor();
            }
        }

        private void SetColor()
        {
            Color Temp = Color.HSVToRGB(floatHue, floatSaturation, floatBrightness, true);
            displayOutput.material.SetColor(ColorName, Temp);
            if (team1Blue)
            {
                PoolTable.tableBlue = Temp;
            }
            else
            {
                PoolTable.tableOrange = Temp;
            }
        }

        public override void OnDeserialization()
        {
            SetColor();
        }

        public void _PrefabPicker(float Hue, float Saturation, float Brightness)
        {
            if (isPanelEnabled)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                floatHue = Hue;
                floatSaturation = Saturation;
                floatBrightness = Brightness;
                RequestSerialization();
                SetColor();
                _PlayAudio(); //not sure if should be networked or not

            }

        }

        public void _EnableDisable(bool IO)
        {
            isPanelEnabled = IO;
            for (int i = 0; i < colorButtons.Length; i++)
            {
                colorButtons[i]._ButtonColors(IO);
            }

        }

        public void _PlayAudio()
        {
            audioSource.Play();
        }
    }
}
