
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
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class colorpicker : UdonSharpBehaviour
    {
        [Header("Interface with the core code")]
        public PoolStateManager PoolTable;
        public bool solidsBlue = true;
        private bool isPanelEnabled = false;
        
        [Header("Shader property name and the display material that will be colored")]
        public string ColorMaterialName = "_Color";
        private Renderer displayOutput;
        
        [Header("HSV values from the Slider and the synced float variables")]
        private Slider[] slidersAll; //0 Hue, 1 Saturation, 2 Brightness
        [UdonSynced()] public float floatHue = 0;
        [UdonSynced()] public float floatSaturation = 0.75f;
        [UdonSynced()] public float floatBrightness = 1;
        
        [Header("The Colored Buttons that are already set up fro the player")]
        private PrefabColor[] colorButtons;
        [Header("The sound that plays when the buttons are pressed")]
        private AudioSource audioSource;

        private void Start()
        {
            slidersAll = GetComponentsInChildren<Slider>();
            displayOutput = GetComponentInChildren<Renderer>();
            colorButtons = GetComponentsInChildren<PrefabColor>();
            audioSource = GetComponentInChildren<AudioSource>();
            if (slidersAll.Length != 3)
            {
                Debug.Log("sliders did not equal 3");
            }
            Color Temp = new Color(floatHue, floatSaturation, floatBrightness, 1);
            displayOutput.material.SetColor(ColorMaterialName, Temp);
            SetColor();
        }
        
        


        public void _SetHue()
        {

            if (isPanelEnabled)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                floatHue = slidersAll[0].value;
                RequestSerialization();
                SetColor();
            }
        }

        public void _SetSaturation()
        {
            if (isPanelEnabled)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                floatSaturation = slidersAll[1].value;
                RequestSerialization();
                SetColor();
            }
        }

        public void _SetBrightness()
        {
            if (isPanelEnabled)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                floatBrightness = slidersAll[3].value;
                RequestSerialization();
                SetColor();
            }
        }

        private void SetColor()
        {
            Color Temp = Color.HSVToRGB(floatHue, floatSaturation, floatBrightness, true);
            displayOutput.material.SetColor(ColorMaterialName, Temp);
            if (solidsBlue)
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
