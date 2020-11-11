using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace QvPen.Udon
{
    public class Settings : UdonSharpBehaviour
    {
        [SerializeField]
        private TextAsset qvPenVersion;
        [HideInInspector]
        public string VERSION;

        [SerializeField]
        private TextAsset udonSharpVersion;

        [SerializeField]
        private Text information;

        [SerializeField]
        private Transform pensParent;
        [SerializeField]
        private Transform erasersParent;

        [HideInInspector]
        public PenManager[] penManagers;
        [HideInInspector]
        public EraserManager[] eraserManagers;

        // Layer 8 : Interactive
        // Layer 9 : Player
        [SerializeField]
        public int inkLayer = 9;
        [SerializeField]
        public int eraserLayer = 8;

        [SerializeField]
        public Material pcInkMaterial;
        [SerializeField]
        public Material questInkMaterial;

        [HideInInspector]
        public readonly string inkPrefix = "Ink";
        [HideInInspector]
        public string inkPoolName;

        [SerializeField]
        public float inkWidth = 0.005f;

        [SerializeField]
        public Shader roundedTrail;

        private void Start()
        {
            VERSION = qvPenVersion.text.Trim();
            Debug.Log($"{nameof(QvPen)} {VERSION}");

            if (udonSharpVersion)
            {
                Debug.Log($"Written in {nameof(UdonSharp)} {udonSharpVersion.text.Trim()} - Merlin");
            }

            information.text += $"\n<size=14>{VERSION}</size>";

            inkPoolName = $"obj_{Guid.NewGuid()}";

            penManagers = pensParent.GetComponentsInChildren<PenManager>();
            eraserManagers = erasersParent.GetComponentsInChildren<EraserManager>();

            foreach (var penManager in penManagers)
            {
                penManager.Boot(this);
            }

            foreach (var eraserManager in eraserManagers)
            {
                eraserManager.Boot(this);
            }
        }
    }
}
