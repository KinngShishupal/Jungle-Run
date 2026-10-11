using System.Collections.Generic;
using UnityEngine;

namespace JungleEscape
{
    /// <summary>
    /// A troop of cheeky monkeys chasing the player. They stay out of view
    /// normally, rush in close after a stumble, and catch you on a second one.
    /// </summary>
    public class MonkeyChaser : MonoBehaviour
    {
        const float FarDistance = 20f;
        const float CloseDistance = 3f;
        const float CatchDistance = 0.9f;
        const float IdleDistance = 3.2f;
        const float CloseTime = 7f;

        Transform player;
        Transform[] monkeys;
        Transform[] armPivots;
        float[] phases;
        float distance = IdleDistance;
        float closeTimer;
        bool caught;
        bool idle = true;

        public bool IsClose => !idle && closeTimer > 0f;

        public void Init(Transform target)
        {
            player = target;
            Build();
        }

        public void Idle() { idle = true; caught = false; closeTimer = 0f; distance = IdleDistance; }
        public void BeginChase() { idle = false; caught = false; closeTimer = 2f; distance = IdleDistance; }
        public void Approach() { closeTimer = CloseTime; }
        public void Catch() { caught = true; }
        public void FallBack() { caught = false; closeTimer = 0f; }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || player == null) return;
            float dt = Time.deltaTime;

            if (closeTimer > 0f && gm.State == GameState.Playing) closeTimer -= dt;

            float target = idle ? IdleDistance
                         : caught ? CatchDistance
                         : closeTimer > 0f ? CloseDistance
                         : FarDistance;
            float rate = caught ? 6f : 1.5f;
            distance = Mathf.Lerp(distance, target, 1f - Mathf.Exp(-rate * dt));

            var p = player.position;
            transform.position = new Vector3(p.x * 0.85f, 0f, p.z - distance);

            // Hop + wave arms
            bool calm = idle || gm.State == GameState.Paused;
            float speed = calm ? 4f : 11f;
            float height = calm ? 0.12f : 0.5f;
            for (int i = 0; i < monkeys.Length; i++)
            {
                float s = Time.time * speed + phases[i];
                var lp = monkeys[i].localPosition;
                monkeys[i].localPosition = new Vector3(lp.x, Mathf.Abs(Mathf.Sin(s)) * height, lp.z);
                armPivots[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(s * 2f) * 20f);
            }
        }

        void Build()
        {
            var fur = Mats.Fur(new Color(0.38f, 0.25f, 0.15f));
            var furDark = Mats.Fur(new Color(0.17f, 0.11f, 0.07f));
            var belly = Mats.Fur(new Color(0.66f, 0.52f, 0.36f));
            var face = Mats.Skin(new Color(0.80f, 0.62f, 0.47f));
            var eye = Mats.Plain(new Color(0.04f, 0.03f, 0.02f), 0.95f);

            var ball = Meshes.Ball(12, 16);
            var limb = Meshes.Limb(0.55f, 0.07f, 0.05f, 8, 7);
            var leg = Meshes.Limb(0.4f, 0.08f, 0.055f, 8, 6, 0.08f);
            var tp = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0.05f, -0.22f), new Vector3(0.03f, 0.25f, -0.42f),
                new Vector3(0.05f, 0.5f, -0.46f), new Vector3(0.04f, 0.7f, -0.36f), new Vector3(0f, 0.8f, -0.2f)
            };
            var tail = Meshes.Sweep(tp, (t, a) => 0.045f * (1f - 0.5f * t), 6, 1f, 2f, true);

            Vector3[] spots = { new Vector3(-1.3f, 0f, 0.3f), new Vector3(0f, 0f, -0.6f), new Vector3(1.3f, 0f, 0.1f) };

            monkeys = new Transform[spots.Length];
            armPivots = new Transform[spots.Length];
            phases = new float[spots.Length];

            for (int i = 0; i < spots.Length; i++)
            {
                var m = Prim.Group("Monkey" + i, transform, spots[i]);
                monkeys[i] = m;
                phases[i] = i * 1.7f;

                // Body leaning into the chase (faces +Z, toward the player)
                Prim.Shape(ball, "Body", m, new Vector3(0f, 0.62f, 0f), new Vector3(0.5f, 0.62f, 0.46f), fur, new Vector3(15f, 0f, 0f));
                Prim.Shape(ball, "Belly", m, new Vector3(0f, 0.6f, 0.15f), new Vector3(0.34f, 0.46f, 0.2f), belly, new Vector3(15f, 0f, 0f));

                // Head
                Prim.Shape(ball, "Head", m, new Vector3(0f, 1.07f, 0.08f), new Vector3(0.42f, 0.4f, 0.4f), fur);
                Prim.Shape(ball, "Cap", m, new Vector3(0f, 1.16f, 0.04f), new Vector3(0.43f, 0.24f, 0.4f), furDark);
                Prim.Shape(ball, "Face", m, new Vector3(0f, 1.05f, 0.21f), new Vector3(0.3f, 0.28f, 0.16f), face);
                Prim.Shape(ball, "Muzzle", m, new Vector3(0f, 0.98f, 0.27f), new Vector3(0.2f, 0.13f, 0.14f), face);
                Prim.Shape(ball, "EyeL", m, new Vector3(-0.065f, 1.09f, 0.29f), Vector3.one * 0.055f, eye, null, false);
                Prim.Shape(ball, "EyeR", m, new Vector3(0.065f, 1.09f, 0.29f), Vector3.one * 0.055f, eye, null, false);
                Prim.Shape(ball, "EarL", m, new Vector3(-0.22f, 1.08f, 0.05f), new Vector3(0.11f, 0.13f, 0.05f), face, new Vector3(0f, -20f, 0f));
                Prim.Shape(ball, "EarR", m, new Vector3(0.22f, 1.08f, 0.05f), new Vector3(0.11f, 0.13f, 0.05f), face, new Vector3(0f, 20f, 0f));

                // Legs, feet and a curling tail
                for (int s = -1; s <= 1; s += 2)
                {
                    var hip = Prim.Group("Hip", m, new Vector3(s * 0.14f, 0.42f, 0f));
                    hip.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                    Prim.Shape(leg, "Leg", hip, Vector3.zero, Vector3.one, fur);
                    Prim.Shape(ball, "Foot", hip, new Vector3(0f, -0.4f, 0.06f), new Vector3(0.1f, 0.07f, 0.17f), furDark);
                }
                Prim.Shape(tail, "Tail", m, new Vector3(0f, 0.45f, -0.2f), Vector3.one, fur);

                // Long arms waving overhead
                var arms = Prim.Group("Arms", m, new Vector3(0f, 0.85f, 0.05f));
                armPivots[i] = arms;
                for (int s = -1; s <= 1; s += 2)
                {
                    var shoulder = Prim.Group("Shoulder", arms, new Vector3(s * 0.2f, 0.08f, 0f));
                    shoulder.localRotation = Quaternion.Euler(0f, 0f, s * 145f);
                    Prim.Shape(limb, "Arm", shoulder, Vector3.zero, Vector3.one, fur);
                    Prim.Shape(ball, "Hand", shoulder, new Vector3(0f, -0.57f, 0f), new Vector3(0.1f, 0.12f, 0.08f), furDark);
                }
            }
        }
    }
}
