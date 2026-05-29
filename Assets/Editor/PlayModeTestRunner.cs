using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Collections.Generic;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";

        private static readonly int WaitFrames = 60;
        private static readonly float TestTimeout = 15.0f;

        private static List<string> _capturedLogs = new List<string>();
        private static Vector3 _startPos;
        private static Vector3 _startScale;

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            if (state == "WaitingForCompile") {
                EditorApplication.delayCall += () => {
                    SessionState.SetString(StateKey, "EnteringPlayMode");
                    EditorApplication.isPlaying = true;
                };
            } else if (state == "EnteringPlayMode" && EditorApplication.isPlaying) {
                SessionState.SetString(StateKey, "InPlayMode");
                EditorApplication.update += WaitFramesThenRun;
            } else if (state == "InPlayMode" && EditorApplication.isPlaying) {
                EditorApplication.update += WaitFramesThenRun;
            } else if (state == "Done") {
                EditorApplication.delayCall += SelfDestruct;
            }
        }

        private static int _frameCount = 0;
        private static bool _setupDone = false;
        private static double _testStartTime = 0;

        private static void WaitFramesThenRun()
        {
            _frameCount++;
            if (_frameCount < WaitFrames) return;

            if (!_setupDone)
            {
                _setupDone = true;
                Application.logMessageReceived += OnLogMessage;
                _testStartTime = EditorApplication.timeSinceStartup;
                Setup();
                return;
            }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _testStartTime);
            if (elapsed >= 3.0f) FinishTest();
        }

        private static void Setup()
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                _startPos = player.transform.position;
                _startScale = player.transform.localScale;
                var keyboard = InputSystem.GetDevice<Keyboard>();
                if (keyboard != null)
                {
                    using (StateEvent.From(keyboard, out var eventPtr))
                    {
                        keyboard.aKey.WriteValueIntoEvent(1f, eventPtr);
                        InputSystem.QueueEvent(eventPtr);
                    }
                    Debug.Log("[Test] A key press simulated");
                }
            }
        }

        private static void FinishTest()
        {
            EditorApplication.update -= WaitFramesThenRun;
            Application.logMessageReceived -= OnLogMessage;

            GameObject player = GameObject.Find("Player");
            float distance = player != null ? Vector3.Distance(_startPos, player.transform.position) : 0f;
            float endScaleX = player != null ? player.transform.localScale.x : 0f;

            var res = new TestResult {
                success = distance > 0.1f && endScaleX < 0,
                distance = distance,
                finalScaleX = endScaleX,
                logs = _capturedLogs.ToArray()
            };
            SessionState.SetString(ResultKey, JsonUtility.ToJson(res));
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_capturedLogs.Count < 50) _capturedLogs.Add("[" + type + "] " + message);
        }

        private static void SelfDestruct()
        {
            AssetDatabase.DeleteAsset(SessionState.GetString(ScriptPathKey, ""));
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        [System.Serializable]
        private class TestResult {
            public bool success;
            public float distance;
            public float finalScaleX;
            public string[] logs;
        }
    }
}
