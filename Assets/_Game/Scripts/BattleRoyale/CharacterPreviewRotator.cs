using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleRoyale
{
    public sealed class CharacterPreviewRotator : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Transform characterRoot;
        [SerializeField, Range(0.05f, 2f)] private float degreesPerPixel = 0.28f;
        [SerializeField] private bool invert;

        private bool dragging;

        public void Configure(Transform target, float sensitivity = 0.28f)
        {
            characterRoot = target;
            degreesPerPixel = Mathf.Clamp(sensitivity, 0.05f, 2f);
        }

        public void OnBeginDrag(PointerEventData eventData) => dragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || characterRoot == null) return;
            characterRoot.Rotate(0f, RotationDelta(eventData.delta.x, degreesPerPixel, invert), 0f, Space.Self);
        }

        public void OnEndDrag(PointerEventData eventData) => dragging = false;

        public static float RotationDelta(float horizontalPixels, float sensitivity, bool inverted) =>
            horizontalPixels * Mathf.Max(0f, sensitivity) * (inverted ? -1f : 1f);
    }
}
