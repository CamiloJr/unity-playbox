#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PiXR.EditorTools.Playbox
{
    public class PlayBoxWindow : EditorWindow
    {
        private string[] _ports = new string[0];
        private int _portIndex = -1;
        private readonly int[] _baudOptions = new[] { 9600, 19200, 38400, 57600, 115200, 230400, 460800 };
        private int _baudIndex = 4;

        private bool _systemEnabled;
        private bool _autoConnect;
        private bool _manualHold;
        private string _lastCmd = "-";
        private bool _isConnected;
        private bool _isConnecting;

        [MenuItem("PiXR/Editor/PlayBox")]
        public static void ShowWindow()
        {
            var win = GetWindow<PlayBoxWindow>("PiXR PlayBox");
            win.minSize = new Vector2(420, 300);
            win.Show();
        }

        private void OnEnable()
        {
            PullStatus();
            RefreshPorts(matchOnly: true, savedPort: PlayBoxService.GetStatus().port);
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void PullStatus()
        {
            var s = PlayBoxService.GetStatus();
            _systemEnabled = s.systemEnabled;
            _autoConnect = s.autoConnect;
            _manualHold = s.manualHold;
            _isConnected = s.isConnected;
            _isConnecting = s.isConnecting;
            _lastCmd = s.lastCmd;

            _baudIndex = System.Array.IndexOf(_baudOptions, s.baud);
            if (_baudIndex < 0) _baudIndex = 4;
        }

        private void OnGUI()
        {
            GUILayout.Label("Serial -> Unity Play Controls", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool newEnabled = EditorGUILayout.ToggleLeft("System enabled", _systemEnabled);
                if (newEnabled != _systemEnabled)
                {
                    _systemEnabled = newEnabled;
                    PlayBoxService.SetSystemEnabled(_systemEnabled);
                    PullStatus();
                    RefreshPorts(matchOnly: true, savedPort: PlayBoxService.GetStatus().port);
                }

                using (new EditorGUI.DisabledScope(!_systemEnabled))
                {
                    EditorGUILayout.LabelField("Serial Port", EditorStyles.label);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string shown = (_portIndex >= 0 && _portIndex < _ports.Length)
                            ? _ports[_portIndex]
                            : "(selected port missing)";

                        if (GUILayout.Button(shown, EditorStyles.popup))
                        {
                            var m = new GenericMenu();
                            if (_ports.Length == 0) m.AddDisabledItem(new GUIContent("No ports found"));
                            else
                            {
                                for (int i = 0; i < _ports.Length; i++)
                                {
                                    int idx = i;
                                    m.AddItem(new GUIContent(_ports[i]), i == _portIndex, () =>
                                    {
                                        _portIndex = idx;
                                        PushConfig();
                                    });
                                }
                            }
                            m.DropDown(GUILayoutUtility.GetLastRect());
                        }

                        if (GUILayout.Button("Reload", GUILayout.Width(100)))
                        {
                            RefreshPorts(matchOnly: true, savedPort: PlayBoxService.GetStatus().port);
                        }
                    }

                    int nb = EditorGUILayout.Popup("Baud rate", _baudIndex, System.Array.ConvertAll(_baudOptions, b => b.ToString()));
                    if (nb != _baudIndex)
                    {
                        _baudIndex = nb;
                        PushConfig();
                    }

                    bool na = EditorGUILayout.ToggleLeft("Auto-reconnect (disconnects/reload - never on project open)", _autoConnect);
                    if (na != _autoConnect)
                    {
                        _autoConnect = na;
                        PushConfig();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_systemEnabled))
                {
                    if (GUILayout.Button(_isConnected ? "Disconnect" : "Connect"))
                    {
                        if (_isConnected) PlayBoxService.Disconnect();
                        else PlayBoxService.Connect();
                        PullStatus();
                    }

                    if (GUILayout.Button("Reconnect now", GUILayout.Width(150)))
                    {
                        PlayBoxService.Disconnect();
                        PlayBoxService.Connect();
                        PullStatus();
                    }
                }
            }

            EditorGUILayout.Space();

            string state = !_systemEnabled
                ? "Disabled"
                : _isConnecting ? "Connecting..."
                : _isConnected ? "Connected"
                : _manualHold ? "Disconnected (manual hold)"
                : "Disconnected";

            EditorGUILayout.LabelField("Status:", state);
            EditorGUILayout.LabelField("Last command:", _lastCmd);

            if (_systemEnabled && (_portIndex < 0 || _portIndex >= _ports.Length))
            {
                EditorGUILayout.HelpBox(
                    "The selected port is not present in the current list. No connection attempt will be made until you click Connect.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Commands (ending with \\n):\n" +
                "down_a -> Play/Stop (instant)\n" +
                "down_b -> Restart\n" +
                "down_c -> Pause/Unpause\n\n" +
                "Tip: For zero drops when entering Play, enable:\n" +
                "Edit -> Project Settings -> Editor -> Enter Play Mode Options -> Disable Domain Reload",
                MessageType.Info);

            PullStatus();
        }

        private void RefreshPorts(bool matchOnly, string savedPort)
        {
            _ports = PlayBoxService.GetPorts();
            _portIndex = string.IsNullOrEmpty(savedPort) ? -1 : System.Array.IndexOf(_ports, savedPort);

            if (!matchOnly)
                PushConfig();

            Repaint();
        }

        private void PushConfig()
        {
            string selectedPort = (_portIndex >= 0 && _portIndex < _ports.Length) ? _ports[_portIndex] : PlayBoxService.GetStatus().port;
            int baud = _baudOptions[Mathf.Clamp(_baudIndex, 0, _baudOptions.Length - 1)];
            PlayBoxService.Configure(selectedPort, baud, _autoConnect);
        }
    }
}
#endif
