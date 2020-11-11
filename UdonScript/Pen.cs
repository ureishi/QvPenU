using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace QvPen.Udon
{
    public class Pen : UdonSharpBehaviour
    {
        [SerializeField]
        private GameObject inkPrefab;
        [SerializeField]
        private GameObject inkCollider;
        [SerializeField]
        private Eraser eraser;

        [SerializeField]
        private Transform inkPosition;
        [SerializeField]
        private Transform spawnTarget;
        [SerializeField]
        private Transform inkPool;

        [SerializeField]
        private float followSpeed = 32;

        private bool isUser;
        private VRC_Pickup pickup;

        // PenManager
        private PenManager penManager;

        // Ink
        private GameObject inkInstance;
        private GameObject justBeforeInk;
        private int inkCount;

        // Eraser
        private readonly float eraserScale = 0.2f;

        // Double click
        private bool useDoubleClick = true;
        private readonly float clickInterval = 0.184f;
        private float prevClickTime;

        // State
        private const int StatePenIdle = 0;
        private const int StatePenUsing = 1;
        private const int StateEraserIdle = 2;
        private const int StateEraserUsing = 3;
        private int currentState = StatePenIdle;

        private int inkLayer;
        private string inkPrefix;
        private string inkPoolName;
        private float inkWidth;

        public void Init(PenManager penManager, Settings settings)
        {
            inkLayer = settings.inkLayer;
            inkPrefix = settings.inkPrefix;
            inkPoolName = settings.inkPoolName;
            inkWidth = settings.inkWidth;

            this.penManager = penManager;

            inkCollider.layer = inkLayer;
            inkPool.name = inkPoolName;

            inkPrefab.SetActive(false);

            var trailRenderer = inkPrefab.GetComponent<TrailRenderer>();
            trailRenderer.emitting = true;
#if UNITY_ANDROID
            var material = settings.questInkMaterial;
            trailRenderer.widthMultiplier = inkWidth;
#else
            var material = settings.pcInkMaterial;
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

            pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            pickup.InteractionText = nameof(Pen);
            pickup.UseText = "Draw";

            // Wait for class inheritance
            // PenManager : Manager, EraserManager : Manager, Init(Manager manager)
            eraser.Init(null, settings);

            eraser.gameObject.SetActive(false);
            eraser.transform.SetParent(inkPosition);
            eraser.transform.localPosition = Vector3.zero;
            eraser.transform.localRotation = Quaternion.identity;
            eraser.transform.localScale = Vector3.one * eraserScale;
        }

        private void LateUpdate()
        {
            if (isUser)
            {
                spawnTarget.position = Vector3.Lerp(spawnTarget.position, inkPosition.position, Time.deltaTime * followSpeed);
                spawnTarget.rotation = Quaternion.Lerp(spawnTarget.rotation, inkPosition.rotation, Time.deltaTime * followSpeed);
            }
            else
            {
                spawnTarget.position = inkPosition.position;
                spawnTarget.rotation = inkPosition.rotation;
            }
        }

        #region Events

        public override void OnPickup()
        {
            isUser = true;
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

        #endregion

        public void DestroyJustBeforeInk()
        {
            Destroy(justBeforeInk);
        }

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

        public bool IsHeld()
        {
            return pickup.IsHeld;
        }

        public void Respawn()
        {
            pickup.Drop();
            if (Networking.LocalPlayer.IsOwner(gameObject))
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }

        public void Clear()
        {
            for (var i = 0; i < inkPool.childCount; i++)
            {
                Destroy(inkPool.GetChild(i).gameObject);
            }
        }

        private void StartDrawing()
        {
            inkInstance = VRCInstantiate(inkPrefab);
            inkInstance.name = "InkMesh";
            inkInstance.transform.SetParent(spawnTarget);
            inkInstance.transform.localPosition = Vector3.zero;
            inkInstance.transform.localRotation = Quaternion.identity;
            inkInstance.transform.localScale = Vector3.one;
            inkInstance.GetComponent<TrailRenderer>().enabled = true;
            inkInstance.SetActive(true);
        }

        private void FinishDrawing()
        {
            if (inkInstance != null)
            {
                inkInstance.name = $"{inkPrefix} ({inkCount++})";
                inkInstance.transform.SetParent(inkPool);

                var inkColliderInstance = VRCInstantiate(inkCollider);
                inkColliderInstance.name = "inkCollider";
                inkColliderInstance.transform.SetParent(inkInstance.transform);
                inkColliderInstance.transform.localPosition = Vector3.zero;
                inkColliderInstance.transform.localRotation = Quaternion.identity;
                inkColliderInstance.transform.localScale = Vector3.one;

                CreateInkCollider(inkColliderInstance);
            }

            justBeforeInk = inkInstance;
            inkInstance = null;
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

        private void CreateInkCollider(GameObject inkCollider)
        {
            var meshCollider = inkCollider.GetComponent<MeshCollider>();

            var trailRenderer = inkInstance.GetComponent<TrailRenderer>();

            var positionCount = Mathf.Max(trailRenderer.positionCount, 2);

            const int verticesPerPoint = 3;
            const int trianglesPerPoint = 3;
            var positions = new Vector3[positionCount];
            var vertices = new Vector3[positionCount * verticesPerPoint];
            var triangles = new int[positionCount * trianglesPerPoint];

            var colliderWidth = inkWidth;

            trailRenderer.GetPositions(positions);
            if (positionCount == 2)
            {
                var offsetZ = Vector3.forward * colliderWidth / 2f;
                positions[0] = inkCollider.transform.position - offsetZ;
                positions[1] = inkCollider.transform.position + offsetZ;
            }

            // Create vertices
            var p0 = inkCollider.transform.InverseTransformPoint(positions[0]);
            for (var i = 0; i < positionCount - 1; i++)
            {
                var p1 = inkCollider.transform.InverseTransformPoint(positions[i + 1]);

                var v = p1 - p0;
                var x = Vector3.ProjectOnPlane(Vector3.right, v);

                if (x == Vector3.zero)
                {
                    x = Vector3.ProjectOnPlane(Vector3.forward, v);
                }

                x = x.normalized * colliderWidth / 2f;

                vertices[i * verticesPerPoint + 0] = p0 + x;
                vertices[i * verticesPerPoint + 1] = p1;
                vertices[i * verticesPerPoint + 2] = p0 - x;

                p0 = p1;
            }

            // Create triangles
            for (var i = 0; i < positionCount; i++)
            {
                triangles[i * trianglesPerPoint + 0] = i * verticesPerPoint + 0;
                triangles[i * trianglesPerPoint + 1] = i * verticesPerPoint + 1;
                triangles[i * trianglesPerPoint + 2] = i * verticesPerPoint + 2;
            }

            // Create mesh
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;

            mesh.RecalculateNormals();

            meshCollider.sharedMesh = mesh;
        }
    }
}
