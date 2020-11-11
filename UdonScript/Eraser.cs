using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace QvPen.Udon
{
    public class Eraser : UdonSharpBehaviour
    {
        [SerializeField]
        private Material normal;
        [SerializeField]
        private Material erasing;

#pragma warning disable CS0108
        private Renderer renderer;
#pragma warning restore CS0108
        private VRC_Pickup pickup;

        private bool isErasing;

        // EraserManager
        private EraserManager eraserManager;

        private int inkLayer;
        private int eraserLayer;
        private string inkPrefix;
        private string inkPoolName;
        private float inkWidth;

        public void Init(EraserManager eraserManager, Settings settings)
        {
            inkLayer = settings.inkLayer;
            eraserLayer = settings.eraserLayer;
            inkPrefix = settings.inkPrefix;
            inkPoolName = settings.inkPoolName;
            inkWidth = settings.inkWidth;

            this.eraserManager = eraserManager;

            gameObject.layer = eraserLayer;

            renderer = GetComponent<Renderer>();
            if (eraserManager)
            {
                // For stand-alone erasers
                pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
                pickup.InteractionText = nameof(Eraser);
                pickup.UseText = "Erase";
            }
            else
            {
                renderer.sharedMaterial = normal;
            }
        }

        public override void OnPickup()
        {
            eraserManager.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EraserManager.StartUsing));

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnPickupEvent));
        }

        public override void OnDrop()
        {
            eraserManager.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EraserManager.EndUsing));

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnDropEvent));
        }

        public override void OnPickupUseDown()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(StartErasing));
        }

        public override void OnPickupUseUp()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(FinishErasing));
        }

        public void OnPickupEvent()
        {
            renderer.sharedMaterial = normal;
        }

        public void OnDropEvent()
        {
            renderer.sharedMaterial = erasing;
        }

        public void StartErasing()
        {
            isErasing = true;
            renderer.sharedMaterial = erasing;
        }

        public void FinishErasing()
        {
            isErasing = false;
            renderer.sharedMaterial = normal;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (
                isErasing &&
                other &&
                other.gameObject.layer == inkLayer &&
                other.transform.parent &&
                other.transform.parent.name.StartsWith(inkPrefix) &&
                other.transform.parent.parent &&
                other.transform.parent.parent.name == inkPoolName
                )
            {
                Destroy(other.transform.parent.gameObject);
            }
        }

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
    }
}
