using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharpEditor;
using UnityEditor;
#endif

namespace VRCBilliards.UCS
{
    [
        DefaultExecutionOrder(1000),
        UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync),
    ]
    public class UCBilliardsButton : UdonSharpBehaviour
    {
        public PoolMenu poolMenu;
        public string eventName;
        public string playerSlotTextFormat = "{0:0}uc to Play";
        public float price = 150.0f;
        public float prizeMultiplayer = 300.0f;
        public float prizeSinglePlayer = 200.0f;
        public AudioClip paySound, payFailedSound, payBackSound;

        private bool joined, isSinglePlayer;
        private UdonBehaviour udonChips;
        private AudioSource audioSource;
        private float Money
        {
            get
            {
                return (float)udonChips.GetProgramVariable("money");
            }
            set
            {
                udonChips.SetProgramVariable("money", value);
            }
        }

        private void Start()
        {
            udonChips = (UdonBehaviour)GameObject.Find("UdonChips").GetComponent(typeof(UdonBehaviour));
            poolMenu.defaultEmptyPlayerSlotText = string.Format(playerSlotTextFormat, price);
            audioSource = poolMenu.manager.GetComponent<AudioSource>();
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip);
        }

        private void OnEnable()
        {
            if (eventName != nameof(PoolMenu._EndGame)) return;

            isSinglePlayer = IsSinglePlayer();
            joined = IsPlayerJoined();
            // Debug.Log($"{transform.parent.parent.gameObject.name}/{transform.parent.gameObject.name}/{gameObject.name}: joined = {joined}/{IsPlayerJoined()}, isWinner={IsWinner()}, IsSinglePlayer={isSinglePlayer}/{IsSinglePlayer()}");
        }

        private void OnDisable()
        {
            if (eventName != nameof(PoolMenu._EndGame) || !joined) return;

            PlaySound(payBackSound);
            Money += isSinglePlayer ? prizeSinglePlayer : prizeMultiplayer;
            // Debug.Log($"{transform.parent.parent.gameObject.name}/{transform.parent.gameObject.name}/{gameObject.name}: joined = {joined}/{IsPlayerJoined()}, isWinner={IsWinner()}, IsSinglePlayer={isSinglePlayer}/{IsSinglePlayer()}");
        }

        private bool IsWinner()
        {
            var winnerText = poolMenu.winnerText.text;
            if (isSinglePlayer && winnerText.Contains("wins!")) return true;

            var displayName = Networking.LocalPlayer.displayName;
            return winnerText.Contains($"{displayName} win") || winnerText.Contains($"{displayName} and");
        }

        private bool IsSinglePlayer()
        {
            var displayName = Networking.LocalPlayer.displayName;
            return poolMenu.player1ScoreText.text == displayName && string.IsNullOrEmpty(poolMenu.player2ScoreText.text) && string.IsNullOrEmpty(poolMenu.player2ScoreText.text);
        }

        private bool IsPlayerJoined()
        {
            var displayName = Networking.LocalPlayer.displayName;
            return poolMenu.player1ScoreText.text == displayName
                || poolMenu.player2ScoreText.text == displayName
                || poolMenu.player3ScoreText.text == displayName
                || poolMenu.player4ScoreText.text == displayName;
        }

        private bool Pay()
        {
            if (Money < price)
            {
                PlaySound(payFailedSound);
                return false;
            }

            PlaySound(paySound);
            Money -= price;
            return true;
        }

        private void PayBack()
        {
            PlaySound(payBackSound);
            Money += price;
        }

        public override void Interact()
        {
            if (poolMenu == null) return;

            if (eventName.StartsWith("SignUpAsPlayer"))
            {
                if (!Pay())
                {
                    return;
                }
            }
            else if (eventName == nameof(PoolMenu.LeaveGame)) PayBack();
            else if (eventName == nameof(PoolMenu._EndGame))
            {
                joined = false;
            }

            poolMenu.SendCustomEvent(eventName);
        }
    }
#if !COMPILER_UDONSHARP && UNITY_EDITOR
    [CustomEditor(typeof(UCBilliardsButton))]
    public class UCBilliardsButtonEditor : Editor
    {
        private readonly string[] eventNames = typeof(PoolMenu).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public).Select(info => info.Name).ToArray();
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            var targetButton = target as UCBilliardsButton;

            serializedObject.Update();

            var property = serializedObject.GetIterator();
            property.NextVisible(true);
            while (property.NextVisible(false))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (property.name == nameof(UCBilliardsButton.eventName))
                    {
                        var index = eventNames.Select((e, i) => (e, i)).Where(t => t.e == property.stringValue).Select(t => t.i).FirstOrDefault();
                        index = EditorGUILayout.Popup(property.displayName, index, eventNames);
                        property.stringValue = eventNames.Skip(index).FirstOrDefault();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }

                    if (property.name == nameof(UCBilliardsButton.poolMenu))
                    {
                        if (GUILayout.Button("Find", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        {
                            property.objectReferenceValue = (target as Component).gameObject?.GetUdonSharpComponentInParent<PoolMenu>();
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            var poolMenu = targetButton?.poolMenu;
            using (new EditorGUI.DisabledGroupScope(poolMenu == null))
            {
                if (GUILayout.Button("Sync price, prize, and sounds"))
                {
                    var price = targetButton.price;
                    var prizeMultiplayer = targetButton.prizeMultiplayer;
                    var prizeSinglePlayer = targetButton.prizeSinglePlayer;
                    var paySound = targetButton.paySound;
                    var payFailedSound = targetButton.payFailedSound;
                    var payBackSound = targetButton.payBackSound;
                    foreach (var button in poolMenu.gameObject.GetUdonSharpComponentsInChildren<UCBilliardsButton>(true))
                    {
                        button.price = price;
                        button.prizeMultiplayer = prizeMultiplayer;
                        button.prizeSinglePlayer = prizeSinglePlayer;
                        button.paySound = paySound;
                        button.payFailedSound = payFailedSound;
                        button.payBackSound = payBackSound;
                        button.ApplyProxyModifications();
                        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(button));
                    }
                }
            }
        }
    }
#endif
}
