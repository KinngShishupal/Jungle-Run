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
        float camYaw;
        bool yawInit;

        void Awake() => cam = GetComponent<Camera>();

        public void Shake(float amount, float time)
        {
            shakeAmount = Mathf.Max(shakeAmount, amount);
            shakeTime = Mathf.Max(shakeTime, time);
        }

        void LateUpdate()
        {
            if (Target == null) return;
            var pc = GameManager.I != null ? GameManager.I.Player : null;
            if (pc == null) return;
            float dt = Time.unscaledDeltaTime;

            // Follow in track space so the camera swings smoothly around bends.
            camX = Mathf.Lerp(camX, pc.TrackX * 0.75f, 1f - Mathf.Exp(-10f * dt));
            camY = Mathf.Lerp(camY, pc.Height * 0.6f, 1f - Mathf.Exp(-5f * dt));
            float camS = pc.TrackS - Back;
            float targetYaw = TrackPath.Yaw(pc.TrackS + 6f);   // look slightly into the bend ahead
            if (!yawInit || Mathf.Abs(Mathf.DeltaAngle(camYaw, targetYaw)) > 60f) { camYaw = targetYaw; yawInit = true; }
            camYaw = Mathf.LerpAngle(camYaw, targetYaw, 1f - Mathf.Exp(-5f * dt));
            var pos = TrackPath.Point(camS, camX, Height + camY);

            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                pos += Random.insideUnitSphere * shakeAmount;
                if (shakeTime <= 0f) shakeAmount = 0f;
            }

            transform.position = pos;
            transform.rotation = Quaternion.Euler(Pitch, camYaw, 0f);

            if (cam != null)
                cam.fieldOfView = Mathf.Clamp(Camera.HorizontalToVerticalFieldOfView(HorizontalFov, cam.aspect), 50f, 95f);
        }
    }
}
