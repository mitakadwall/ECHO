using UnityEngine;

namespace Eco.Enemies.Boss
{
    /// <summary>
    /// Destructible structural column in Pavimento 01 (Câmara de Prensagem).
    /// When Overseer Carcass-0's linear charge slams into this column (e.g. player baited
    /// the charge behind cover), the column shatters and the boss takes 30 self-damage + stun.
    /// </summary>
    public class BreakableColumn : MonoBehaviour
    {
        [SerializeField] private float selfDamageToBoss = 30f;
        [SerializeField] private bool isBroken;

        public bool IsBroken => isBroken;
        public float SelfDamageToBoss => selfDamageToBoss;

        public bool TryBreak(Vector3 impactPoint, Vector3 impactDirection)
        {
            if (isBroken) return false;
            isBroken = true;

            SpawnDebrisBurst(impactPoint);

            foreach (Collider col in GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }

            Debug.Log($"[BreakableColumn] '{name}' destruída pela investida do Carcass-0! Boss sofreu {selfDamageToBoss} de dano.");
            return true;
        }

        private void SpawnDebrisBurst(Vector3 point)
        {
            GameObject fxGo = new GameObject("ColumnBreakDebris");
            fxGo.transform.position = point;

            ParticleSystem ps = fxGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.35f);
            main.startColor = new Color(0.75f, 0.52f, 0.25f, 0.9f);
            main.gravityModifier = 1.4f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.7f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (s == null) s = Shader.Find("Particles/Standard Unlit");
                if (s != null)
                {
                    Material m = new Material(s);
                    m.color = new Color(0.85f, 0.6f, 0.3f, 1f);
                    renderer.sharedMaterial = m;
                }
            }

            ps.Play();
        }
    }
}
