#if UNITY_EDITOR
using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;
using System.Collections.Concurrent;
using UnityEditor;

namespace PiXR.EditorTools.Playbox
{
    [ExecuteAlways]
    public class PlayBoxService : MonoBehaviour
    {
        private const string PREF_ENABLED = "PiXR_PlayBox_Enabled";
        private const string PREF_PORT = "PiXR_PlayBox_Port";
        private const string PREF_BAUD = "PiXR_PlayBox_Baud";
        private const string PREF_AUTOCONNECT = "PiXR_PlayBox_AutoConnect";
        private const string PREF_MANUAL_HOLD = "PiXR_PlayBox_ManualHold";
        private const string PREF_WAS_CONNECTED = "PiXR_PlayBox_WasConnectedBeforeReload";

        private static PlayBoxService _instance;
        private static bool IsSystemEnabledPref => EditorPrefs.GetBool(PREF_ENABLED, false);

        public static PlayBoxService Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PiXR_PlayBoxService");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<PlayBoxService>();
                    _instance.RegisterEditorHooks();
                }
                return _instance;
            }
        }

        [SerializeField] private string _portName = "";
        [SerializeField] private int _baud = 115200;
        [SerializeField] private bool _autoConnect = false;
        [SerializeField] private bool _manualHold = false;

        private SerialPort _serial;
        private Thread _readThread;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();
        private volatile bool _isConnecting;
        private string _lastCmd = "-";
        private static bool _pendingRestart;

        private int _reconnectBackoffMs = 500;
        private DateTime _nextReconnectAt = DateTime.MinValue;

        public bool IsConnected => _serial != null && _serial.IsOpen;
        public bool IsConnecting => _isConnecting;
        public string LastCommand => _lastCmd;
        public string CurrentPort => _portName;
        public int CurrentBaud => _baud;
        public bool AutoConnect => _autoConnect;
        public bool ManualHold => _manualHold;

        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            if (!IsSystemEnabledPref) return;
            var inst = Instance;
            inst.LoadPrefs();

            bool was = EditorPrefs.GetBool(PREF_WAS_CONNECTED, false);
            EditorPrefs.DeleteKey(PREF_WAS_CONNECTED);
            if (was && inst._autoConnect && !inst._manualHold && !inst.IsConnected && !string.IsNullOrEmpty(inst._portName))
            {
                inst.ScheduleReconnectSoon();
            }
        }

        private void RegisterEditorHooks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedInstance;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReloadInstance;
            EditorApplication.update += OnEditorUpdateInstance;
        }

        private void UnregisterEditorHooks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChangedInstance;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReloadInstance;
            EditorApplication.update -= OnEditorUpdateInstance;
        }

        private void OnBeforeAssemblyReloadInstance()
        {
            if (IsConnected) EditorPrefs.SetBool(PREF_WAS_CONNECTED, true);
            InternalDisconnect(closeForReload: true, silent: true);
        }

        private void OnPlayModeStateChangedInstance(PlayModeStateChange change) { }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }
            _instance = this;
            LoadPrefs();
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                UnregisterEditorHooks();
                InternalDisconnect(silent: true);
                _instance = null;
            }
        }

        private void OnEditorUpdateInstance()
        {
            if (!IsSystemEnabledPref) return;
            EditorTick();
        }

        private void Update()
        {
            if (!IsSystemEnabledPref) return;
            EditorTick();
        }

        private void EditorTick()
        {
            while (_inbox.TryDequeue(out var cmd))
            {
                _lastCmd = cmd;
                HandleCommandOnMainThread(cmd);
            }

            if (_pendingRestart && !EditorApplication.isPlaying)
            {
                _pendingRestart = false;
                StartPlay();
            }

            if (_autoConnect && !_manualHold && !_isConnecting && !IsConnected && !string.IsNullOrEmpty(_portName))
            {
                if (DateTime.UtcNow >= _nextReconnectAt)
                    TryConnect();
            }
        }

        public struct Status
        {
            public bool systemEnabled;
            public bool isConnected;
            public bool isConnecting;
            public bool manualHold;
            public bool autoConnect;
            public string port;
            public int baud;
            public string lastCmd;
        }

        public static Status GetStatus()
        {
            var s = new Status
            {
                systemEnabled = IsSystemEnabledPref,
                autoConnect = EditorPrefs.GetBool(PREF_AUTOCONNECT, false),
                manualHold = EditorPrefs.GetBool(PREF_MANUAL_HOLD, false),
                port = EditorPrefs.GetString(PREF_PORT, string.Empty),
                baud = EditorPrefs.GetInt(PREF_BAUD, 115200),
                isConnected = false,
                isConnecting = false,
                lastCmd = "-"
            };

            if (_instance != null)
            {
                s.autoConnect = _instance._autoConnect;
                s.manualHold = _instance._manualHold;
                s.port = _instance._portName;
                s.baud = _instance._baud;
                s.isConnected = _instance.IsConnected;
                s.isConnecting = _instance.IsConnecting;
                s.lastCmd = _instance.LastCommand;
            }

            return s;
        }

        public static void SetSystemEnabled(bool enabled)
        {
            EditorPrefs.SetBool(PREF_ENABLED, enabled);

            if (!enabled)
            {
                if (_instance != null)
                {
                    _instance._manualHold = true;
                    _instance.SavePrefs();
                    _instance.InternalDisconnect(silent: true);
                    _instance.UnregisterEditorHooks();
                    DestroyImmediate(_instance.gameObject);
                    _instance = null;
                }
            }
            else
            {
                if (_instance == null)
                {
                    var inst = Instance;
                    inst.LoadPrefs();
                    inst._manualHold = true;
                    inst.SavePrefs();
                }
            }
        }

        public static void Configure(string portName, int baud, bool autoConnect)
        {
            EditorPrefs.SetString(PREF_PORT, portName ?? "");
            EditorPrefs.SetInt(PREF_BAUD, baud);
            EditorPrefs.SetBool(PREF_AUTOCONNECT, autoConnect);

            if (_instance != null)
            {
                _instance._portName = portName ?? "";
                _instance._baud = baud;
                _instance._autoConnect = autoConnect;
                _instance.SavePrefs();
            }
        }

        public static void Connect()
        {
            if (!IsSystemEnabledPref) return;
            var i = Instance;
            i._manualHold = false;
            i.SavePrefs();
            i.ScheduleReconnectSoon();
        }

        public static void Disconnect()
        {
            if (_instance == null) return;
            _instance._manualHold = true;
            _instance.SavePrefs();
            _instance.InternalDisconnect();
        }

        public static string[] GetPorts()
        {
            try
            {
                var names = SerialPort.GetPortNames();
                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                return names;
            }
            catch (Exception e)
            {
                Debug.LogError($"PlayBoxService: failed to list ports: {e.Message}");
                return Array.Empty<string>();
            }
        }

        private void LoadPrefs()
        {
            _portName = EditorPrefs.GetString(PREF_PORT, _portName);
            _baud = EditorPrefs.GetInt(PREF_BAUD, _baud);
            _autoConnect = EditorPrefs.GetBool(PREF_AUTOCONNECT, _autoConnect);
            _manualHold = EditorPrefs.GetBool(PREF_MANUAL_HOLD, _manualHold);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PREF_PORT, _portName ?? "");
            EditorPrefs.SetInt(PREF_BAUD, _baud);
            EditorPrefs.SetBool(PREF_AUTOCONNECT, _autoConnect);
            EditorPrefs.SetBool(PREF_MANUAL_HOLD, _manualHold);
        }

        private void ScheduleReconnectSoon()
        {
            _nextReconnectAt = DateTime.UtcNow;
        }

        private void TryConnect()
        {
            if (_isConnecting || IsConnected) return;
            if (string.IsNullOrEmpty(_portName))
            {
                Debug.LogWarning("PlayBoxService: port not set.");
                return;
            }

            _isConnecting = true;
            try
            {
                InternalDisconnect(silent: true);

                _cts = new CancellationTokenSource();

                _serial = new SerialPort(_portName, _baud, Parity.None, 8, StopBits.One)
                {
                    Handshake = Handshake.None,
                    ReadTimeout = 100,
                    WriteTimeout = 100,
                    NewLine = "\n",
                    DtrEnable = false,
                    RtsEnable = false
                };

                _serial.Open();

                _readThread = new Thread(() => ReadLoop(_cts.Token))
                {
                    IsBackground = true,
                    Name = "PiXR.PlayBox.SerialReader"
                };
                _readThread.Start();

                Debug.Log($"PlayBoxService: connected to {_portName} @ {_baud} bps");
                _reconnectBackoffMs = 500;
                _nextReconnectAt = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"PlayBoxService: error connecting to '{_portName}': {ex.Message}");
                InternalDisconnect(silent: true);
                _nextReconnectAt = DateTime.UtcNow.AddMilliseconds(_reconnectBackoffMs);
                _reconnectBackoffMs = Math.Min(_reconnectBackoffMs * 2, 8000);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private void InternalDisconnect(bool closeForReload = false, bool silent = false)
        {
            try { _cts?.Cancel(); } catch { }

            if (_readThread != null && _readThread.IsAlive)
            {
                try { _readThread.Join(300); } catch { }
            }

            if (_serial != null)
            {
                try
                {
                    if (_serial.IsOpen) _serial.Close();
                    _serial.Dispose();
                }
                catch { }
            }

            _readThread = null;
            _cts = null;
            _serial = null;

            if (!closeForReload && !silent)
                Debug.Log("PlayBoxService: disconnected.");
        }

        private void ReadLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _serial != null && _serial.IsOpen)
                {
                    try
                    {
                        string line = _serial.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        _inbox.Enqueue(line.Trim().ToLowerInvariant());
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"PlayBoxService: read error: {ex.Message}");
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    if (_serial != null && _serial.IsOpen)
                        _serial.Close();
                }
                catch { }

                if (_autoConnect && !_manualHold && !string.IsNullOrEmpty(_portName))
                {
                    _nextReconnectAt = DateTime.UtcNow.AddMilliseconds(_reconnectBackoffMs);
                    _reconnectBackoffMs = Math.Min(_reconnectBackoffMs * 2, 8000);
                }
            }
        }

        private static void HandleCommandOnMainThread(string cmd)
        {
            switch (cmd)
            {
                case "down_a":
                    Debug.Log("PlayBoxService: down_a (Play/Stop)");
                    TogglePlay();
                    break;

                case "down_b":
                    Debug.Log("PlayBoxService: down_b (Restart)");
                    Restart();
                    break;

                case "down_c":
                    Debug.Log("PlayBoxService: down_c (Pause/Unpause)");
                    TogglePause();
                    break;

                default:
                    Debug.Log($"PlayBoxService: unknown command: '{cmd}'");
                    break;
            }
        }

        private static void TogglePlay()
        {
            EditorApplication.ExecuteMenuItem("Edit/Play");
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private static void Restart()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExecuteMenuItem("Edit/Play");
            }
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Edit/Play");
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            };
        }

        private static void TogglePause()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("PlayBoxService: ignoring Pause - not playing.");
                return;
            }
            EditorApplication.ExecuteMenuItem("Edit/Pause");
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private static void StartPlay()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.ExecuteMenuItem("Edit/Play");
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }
    }
}
#endif
