using UdonSharp;
using UnityEngine;

namespace FairlySadPanda
{
    namespace UsefulThings
    {
        [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
        public class Logger : UdonSharpBehaviour
        {
            public TMPro.TextMeshProUGUI text;
            public int maxChars;

            [Tooltip("Print Log calls to console. Enable to make logs easier to see.")]
            public bool printLogsToConsole;

            public void Start()
            {
                _Log("TestLogger", "Start");
            }

            public void _Log(string source, string log)
            {
                if (printLogsToConsole)
                {
                    Debug.Log($"[{Time.timeSinceLevelLoad:N2}] [<color=green>{source}</color>] {log}");
                }

                text.text += $"\n[{Time.timeSinceLevelLoad:N2}] [<color=green>{source}</color>] {log}";
                while (text.text.Length > maxChars && text.text.Contains("\n"))
                {
                    text.text = text.text.Substring(text.text.IndexOf("\n") + 1);
                }
            }

            public void _Warning(string source, string log)
            {
                Debug.LogWarning($"[{Time.timeSinceLevelLoad:N2}] [<color=red>{source}</color>] {log}");
                text.text += $"\n[{Time.timeSinceLevelLoad:N2}] [<color=yellow>{source}</color>] {log}";
                while (text.text.Length > maxChars && text.text.Contains("\n"))
                {
                    text.text = text.text.Substring(text.text.IndexOf("\n") + 1);
                }
            }

            public void _Error(string source, string log)
            {
                Debug.LogError($"[{Time.timeSinceLevelLoad:N2}] [<color=red>{source}</color>] {log}");
                text.text += $"\n[{Time.timeSinceLevelLoad:N2}] [<color=red>{source}</color>] {log}";
                while (text.text.Length > maxChars && text.text.Contains("\n"))
                {
                    text.text = text.text.Substring(text.text.IndexOf("\n") + 1);
                }
            }

            public void Clear()
            {
                text.text = string.Empty;
            }
        }
    }
}