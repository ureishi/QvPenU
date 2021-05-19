using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.SDK3.Components;
using VRC.Udon.Common.Interfaces;

namespace QvPen.Udon
{
    public class Pen : UdonSharpBehaviour
    {
        [SerializeField]
        private Material
            pcInkMaterial,
            questInkMaterial;

        [SerializeField]
        private TrailRenderer
            trailRenderer;
        [SerializeField]
        private LineRenderer
            linePrefab;
        [SerializeField]
        private MeshCollider
            colliderPrefab;
        [SerializeField]
        private Eraser
            eraser;

        private GameObject
            lineInstance;

        [SerializeField]
        private Transform
            inkPosition;
        [SerializeField]
        private Transform
            inkPool;

        [SerializeField]
        private float
            followSpeed = 32;

        // Components
        private bool
            isUser;
        private VRC_Pickup
            pickup;
        private VRCObjectSync
            objectSync;

        // PenManager
        private PenManager
            penManager;

        // Ink
        private GameObject
            justBeforeInk;
        private int
            inkNo;

        // Eraser
        private readonly float
            eraserScale = 0.2f;

        // Double click
        private bool
            useDoubleClick = true;
        private readonly float
            clickInterval = 0.184f;
        private float
            prevClickTime;

        // State
        private const int
            StatePenIdle = 0,
            StatePenUsing = 1,
            StateEraserIdle = 2,
            StateEraserUsing = 3;
        private int
            currentState = StatePenIdle;

        private int
            inkLayer;
        private string
            inkPrefix;
        private string
            inkPoolName;
        private float
            inkWidth;

        public void Init(PenManager penManager, Settings settings)
        {
            this.penManager = penManager;

            inkLayer = settings.inkLayer;
            inkPrefix = settings.inkPrefix;
            inkPoolName = settings.inkPoolName;
            inkWidth = settings.inkWidth;

            colliderPrefab.gameObject.layer = inkLayer;
            inkPool.name = inkPoolName;

            {
#if UNITY_ANDROID
                var material = settings.questInkMaterial;
                trailRenderer.widthMultiplier = inkWidth;
#else
                var material = pcInkMaterial;
                if (material.shader == settings.roundedTrail)
                {
                    trailRenderer.widthMultiplier = 0f;
                    material.SetFloat("_Width", inkWidth);
                }
                else
                {
                    trailRenderer.widthMultiplier = inkWidth;
                }
#endif
                trailRenderer.material = material;
                trailRenderer.colorGradient = penManager.colorGradient;
            }

            {
#if UNITY_ANDROID
                var material = settings.questInkMaterial;
                linePrefab.widthMultiplier = inkWidth;
#else
                var material = pcInkMaterial;
                if (material.shader == settings.roundedTrail)
                {
                    linePrefab.widthMultiplier = 0f;
                    material.SetFloat("_Width", inkWidth);
                }
                else
                {
                    linePrefab.widthMultiplier = inkWidth;
                }
#endif
                linePrefab.material = material;
                linePrefab.colorGradient = penManager.colorGradient;
            }

            pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            pickup.InteractionText = nameof(Pen);
            pickup.UseText = "Draw";

            objectSync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));

            settings.inkPool = inkPool;
            eraser.Init(null, settings);
            eraser.gameObject.SetActive(false);
            eraser.transform.SetParent(inkPosition);
            eraser.transform.localPosition = Vector3.zero;
            eraser.transform.localRotation = Quaternion.identity;
            eraser.transform.localScale = Vector3.one * eraserScale;
        }

        private void LateUpdate()
        {
            if (!pickup.IsHeld)
                return;

            if (isUser)
            {
                trailRenderer.transform.position = Vector3.Lerp(trailRenderer.transform.position, inkPosition.position, Time.deltaTime * followSpeed);
                trailRenderer.transform.rotation = Quaternion.Lerp(trailRenderer.transform.rotation, inkPosition.rotation, Time.deltaTime * followSpeed);
            }
            else
            {
                trailRenderer.transform.position = inkPosition.position;
                trailRenderer.transform.rotation = inkPosition.rotation;
            }
        }

        #region Events

        public override void OnPickup()
        {
            isUser = true;

            if (!Networking.IsOwner(penManager.gameObject))
                Networking.SetOwner(Networking.LocalPlayer, penManager.gameObject);

            penManager.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PenManager.StartUsing));

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToPenIdle));
        }

        public override void OnDrop()
        {
            isUser = false;

            penManager.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PenManager.EndUsing));

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToPenIdle));
        }

        public override void OnPickupUseDown()
        {
            if (useDoubleClick && Time.time - prevClickTime < clickInterval)
            {
                prevClickTime = 0f;
                switch (currentState)
                {
                    case StatePenIdle:
                        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(DestroyJustBeforeInk));
                        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToEraseIdle));
                        break;
                    case StateEraserIdle:
                        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToPenIdle));
                        break;
                    default:
                        Debug.LogError($"Unexpected state : {currentState} at {nameof(OnPickupUseDown)} Double Clicked");
                        break;
                }
            }
            else
            {
                prevClickTime = Time.time;
                switch (currentState)
                {
                    case StatePenIdle:
                        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToPenUsing));
                        break;
                    case StateEraserIdle:
                        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToEraseUsing));
                        break;
                    default:
                        Debug.LogError($"Unexpected state : {currentState} at {nameof(OnPickupUseDown)}");
                        break;
                }
            }
        }

        public override void OnPickupUseUp()
        {
            switch (currentState)
            {
                case StatePenUsing:
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToPenIdle));
                    break;
                case StateEraserUsing:
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToEraseIdle));
                    break;
                case StatePenIdle:
                    Debug.Log($"Change state : {StateEraserIdle} to {currentState}");
                    break;
                case StateEraserIdle:
                    Debug.Log($"Change state : {StatePenIdle} to {currentState}");
                    break;
                default:
                    Debug.Log($"Unexpected state : {currentState} at {nameof(OnPickupUseUp)}");
                    break;
            }
        }

        public void SetUseDoubleClick(bool value)
        {
            useDoubleClick = value;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChangeStateToPenIdle));
        }

        public void DestroyJustBeforeInk()
        {
            Destroy(justBeforeInk);
            inkNo--;
        }

        #endregion

        #region ChangeState

        public void ChangeStateToPenIdle()
        {
            switch (currentState)
            {
                case StatePenUsing:
                    FinishDrawing();
                    break;
                case StateEraserIdle:
                    ChangeToPen();
                    break;
                case StateEraserUsing:
                    FinishErasing();
                    ChangeToPen();
                    break;
            }
            currentState = StatePenIdle;
        }

        public void ChangeStateToPenUsing()
        {
            switch (currentState)
            {
                case StatePenIdle:
                    StartDrawing();
                    break;
                case StateEraserIdle:
                    ChangeToPen();
                    StartDrawing();
                    break;
                case StateEraserUsing:
                    FinishErasing();
                    ChangeToPen();
                    StartDrawing();
                    break;
            }
            currentState = StatePenUsing;
        }

        public void ChangeStateToEraseIdle()
        {
            switch (currentState)
            {
                case StatePenIdle:
                    ChangeToEraser();
                    break;
                case StatePenUsing:
                    FinishDrawing();
                    ChangeToEraser();
                    break;
                case StateEraserUsing:
                    FinishErasing();
                    break;
            }
            currentState = StateEraserIdle;
        }

        public void ChangeStateToEraseUsing()
        {
            switch (currentState)
            {
                case StatePenIdle:
                    ChangeToEraser();
                    StartErasing();
                    break;
                case StatePenUsing:
                    FinishDrawing();
                    ChangeToEraser();
                    StartErasing();
                    break;
                case StateEraserIdle:
                    StartErasing();
                    break;
            }
            currentState = StateEraserUsing;
        }

        #endregion

        public bool IsHeld() => pickup.IsHeld;

        public void Respawn()
        {
            pickup.Drop();

            if (Networking.LocalPlayer.IsOwner(gameObject))
                objectSync.Respawn();
        }

        public void Clear()
        {
            foreach (Transform ink in inkPool)
                Destroy(ink.gameObject);

            inkNo = 0;
        }

        private void StartDrawing()
        {
            trailRenderer.gameObject.SetActive(true);
        }

        private void FinishDrawing()
        {
            P($"{nameof(FinishDrawing)}()");

            if (isUser)
            {
                var positions = new Vector3[trailRenderer.positionCount];

                trailRenderer.GetPositions(positions);

                Array.Reverse(positions);

                penManager.syncPositions = positions;
                penManager.RequestSerialization();

                CreateInkInstance(positions);
            }

            trailRenderer.gameObject.SetActive(false);
            trailRenderer.Clear();
        }

        private void StartErasing()
        {
            eraser.StartErasing();
        }

        private void FinishErasing()
        {
            eraser.FinishErasing();
        }

        private void ChangeToPen()
        {
            eraser.FinishErasing();
            eraser.gameObject.SetActive(false);
        }

        private void ChangeToEraser()
        {
            eraser.gameObject.SetActive(true);
        }

        public void CreateInkInstance(Vector3[] positions)
        {
            if (positions.Length == 0)
                return;

            lineInstance = VRCInstantiate(linePrefab.gameObject);
            lineInstance.name = $"{inkPrefix} ({inkNo++})";
            lineInstance.transform.SetParent(inkPool);
            lineInstance.transform.position = positions[0];
            lineInstance.transform.localRotation = Quaternion.identity;
            lineInstance.transform.localScale = Vector3.one;

            var line = lineInstance.GetComponent<LineRenderer>();
            line.positionCount = positions.Length;
            line.SetPositions(positions);

            CreateInkCollider(line);

            lineInstance.gameObject.SetActive(true);

            justBeforeInk = lineInstance;
        }

        private void CreateInkCollider(LineRenderer lineRenderer)
        {
            P($"{nameof(CreateInkCollider)}(lineRenderer: )");

            var colliderInstance = VRCInstantiate(colliderPrefab.gameObject);
            colliderInstance.name = "InkCollider";
            colliderInstance.transform.SetParent(lineInstance.transform);
            colliderInstance.transform.position = Vector3.zero;
            colliderInstance.transform.rotation = Quaternion.identity;
            colliderInstance.transform.localScale = Vector3.one;

            var meshCollider = colliderInstance.GetComponent<MeshCollider>();

            var mesh = new Mesh();
            var widthMultiplier = lineRenderer.widthMultiplier;

            lineRenderer.widthMultiplier = inkWidth;
            lineRenderer.BakeMesh(mesh);
            lineRenderer.widthMultiplier = widthMultiplier;

            meshCollider.sharedMesh = mesh;
            meshCollider.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        #region Log

        [HideInInspector]
        public readonly string
            appname = $"{nameof(QvPen)}.{nameof(QvPen.Udon)}.{nameof(QvPen.Udon.Pen)}";
        [HideInInspector]
        public string
            version;
        public TextAsset
            versionText;

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
                Debug.Log($"[{CTag(C_APP)}{appname}{CTagEnd}] {CTag(C_LOG)}{o}{CTagEnd}", this);
        }

        private void P_LOG(object o)
        {
            Debug.Log($"[{CTag(C_APP)}{appname}{CTagEnd}] {CTag(C_LOG)}{o}{CTagEnd}", this);
        }

        private void P_WAR(object o)
        {
            Debug.LogWarning($"[{CTag(C_APP)}{appname}{CTagEnd}] {CTag(C_WAR)}{o}{CTagEnd}", this);
        }

        private void P_ERR(object o)
        {
            Debug.LogError($"[{CTag(C_APP)}{appname}{CTagEnd}] {CTag(C_ERR)}{o}{CTagEnd}", this);
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
