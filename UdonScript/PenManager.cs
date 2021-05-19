using System;
using VRC.Udon.Common;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace QvPen.Udon
{
    public class PenManager : UdonSharpBehaviour
    {
        [SerializeField]
        private Pen pen;

        public Gradient colorGradient = new Gradient();

        [SerializeField]
        private GameObject respawnButton;
        [SerializeField]
        private GameObject clearButton;
        [SerializeField]
        private GameObject inUseUI;
        [SerializeField]
        private Text textInUse;

        public void Init(Settings settings)
        {
            // Wait for class inheritance
            pen.Init(this, settings);
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (!Networking.LocalPlayer.IsOwner(pen.gameObject))
                return;

            if (pen.IsHeld())
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(StartUsing));
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Networking.LocalPlayer.IsOwner(pen.gameObject))
                return;

            if (!pen.IsHeld())
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EndUsing));
            }
        }

        public void StartUsing()
        {
            respawnButton.SetActive(false);
            clearButton.SetActive(false);
            inUseUI.SetActive(true);

            var owner = Networking.GetOwner(pen.gameObject);
            textInUse.text = owner != null ? owner.displayName : "Occupied";
        }

        public void EndUsing()
        {
            respawnButton.SetActive(true);
            clearButton.SetActive(true);
            inUseUI.SetActive(false);

            textInUse.text = string.Empty;
        }

        public void ResetAll()
        {
            pen.Respawn();
            pen.Clear();
        }

        public void ClearAll()
        {
            pen.Clear();
        }

        public void SetUseDoubleClick(bool value)
        {
            pen.SetUseDoubleClick(value);
        }

        #region Network

        [UdonSynced, NonSerialized]
        public Vector3[]
            syncPositions = new Vector3[0];

        public void printps()
        {
            foreach (var p in syncPositions) P(p);
        }

        public override void OnPreSerialization()
        {
            P($"{nameof(OnPreSerialization)}()");
            printps();
        }

        public override void OnPostSerialization(SerializationResult result)
        {
            P($"{nameof(OnPostSerialization)}(result: (success: {result.success}, byteCount{result.byteCount:#,0}))");

            if (result.success)
            {

            }
            else
            {

            }
        }

        public override void OnDeserialization()
        {
            P($"{nameof(OnDeserialization)}()");

            printps();

            pen.CreateInkInstance(syncPositions);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            P($"{nameof(OnOwnershipTransferred)}(player: {player.displayName})");
        }

        public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner)
        {
            P($"{nameof(OnOwnershipRequest)}(requestingPlayer: {requestingPlayer.displayName}, requestedOwner: {requestedOwner.displayName})");

            return true;
        }

        #endregion Network


        #region Log

        public readonly string
            className = $"{nameof(QvPen)}.{nameof(QvPen.Udon)}.{nameof(QvPen.Udon.PenManager)}";

        [SerializeField]
        private bool
            doWriteDebugLog = false;

        private Color
            C_APP = new Color(0xf2, 0x7d, 0x4a, 0xff) / 0xff,
            C_LOG = new Color(0x00, 0x8b, 0xca, 0xff) / 0xff,
            C_WAR = new Color(0xfe, 0xeb, 0x5b, 0xff) / 0xff,
            C_ERR = new Color(0xe0, 0x30, 0x5a, 0xff) / 0xff;

        private readonly string
            CTagEnd = "</color>";

        private void P(object o)
        {
            if (doWriteDebugLog)
                Debug.Log($"[{CTag(C_APP)}{className}{CTagEnd}] {CTag(C_LOG)}{o}{CTagEnd}", this);
        }

        private void P_LOG(object o)
        {
            Debug.Log($"[{CTag(C_APP)}{className}{CTagEnd}] {CTag(C_LOG)}{o}{CTagEnd}", this);
        }

        private void P_WAR(object o)
        {
            Debug.LogWarning($"[{CTag(C_APP)}{className}{CTagEnd}] {CTag(C_WAR)}{o}{CTagEnd}", this);
        }

        private void P_ERR(object o)
        {
            Debug.LogError($"[{CTag(C_APP)}{className}{CTagEnd}] {CTag(C_ERR)}{o}{CTagEnd}", this);
        }

        private string CTag(Color c)
        {
            return $"<color=\"#{ToHtmlStringRGB(c)}\">";
        }

        private string ToHtmlStringRGB(Color c)
        {
            c *= 0xff;
            return $"{Mathf.RoundToInt(c.r):x2}{Mathf.RoundToInt(c.g):x2}{Mathf.RoundToInt(c.b):x2}";
        }

        #endregion
    }
}
