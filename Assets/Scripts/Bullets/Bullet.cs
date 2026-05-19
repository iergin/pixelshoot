using UnityEngine;
using DG.Tweening;
using PixelShoot.Data;
using PixelShoot.Grid;

namespace PixelShoot.Bullets
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private float speed = 25f;

        private Box target;
        private GridController gridController;
        private Tween moveTween;

        public void Fire(Vector3 from, Box targetBox, GridController grid, ColorData color)
        {
            target = targetBox;
            gridController = grid;
            transform.position = from;

            if (meshRenderer != null && color != null && color.BulletMaterial != null)
                meshRenderer.sharedMaterial = color.BulletMaterial;

            var endPos = grid.GetCellWorldPosition(target.GridX, target.GridZ);
            float distance = Vector3.Distance(from, endPos);
            float duration = Mathf.Max(0.02f, distance / speed);

            transform.LookAt(endPos);
            moveTween = transform.DOMove(endPos, duration)
                .SetEase(Ease.Linear)
                .OnComplete(OnArrive);
        }

        private void OnArrive()
        {
            if (target != null && target.IsAlive)
                gridController?.NotifyBoxHit(target);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            moveTween?.Kill();
        }
    }
}
