using UnityEngine;

namespace JungleEscape
{
    /// <summary>Chase camera behind the player with smoothing and screen shake.</summary>
    public class CameraFollow : MonoBehaviour
    {
        const float Height = 4.2f;
        const float Back = 7f;
        const float Pitch = 17f;
        const float HorizontalFov = 65f; // keeps all 3 lanes visible in portrait

        public Transform Target;

        Camera cam;
        float camX, camY;
        float shakeTime, shakeAmount;

        void Awake() => cam = GetComponent<Camera>();

        public void Shake(float amount, float time)
        {
            shakeAmount = Mathf.Max(shakeAmount, amount);
            shakeTime = Mathf.Max(shakeTime, time);
        }

        void LateUpdate()
        {
            if (Target == null) return;
            float dt = Time.unscaledDeltaTime;
            var t = Target.position;

            camX = Mathf.Lerp(camX, t.x * 0.75f, 1f - Mathf.Exp(-10f * dt));
            camY = Mathf.Lerp(camY, t.y * 0.6f, 1f - Mathf.Exp(-5f * dt));
            var pos = new Vector3(camX, Height + camY, t.z - Back);

            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                pos += Random.insideUnitSphere * shakeAmount;
                if (shakeTime <= 0f) shakeAmount = 0f;
            }

            transform.position = pos;
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);

            if (cam != null)
                cam.fieldOfView = Mathf.Clamp(Camera.HorizontalToVerticalFieldOfView(HorizontalFov, cam.aspect), 50f, 95f);
        }
    }
}
