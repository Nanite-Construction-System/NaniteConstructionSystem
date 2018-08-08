using System;
using VRageMath;
using VRage.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.Entity;
using Sandbox.Game.Entities;
using NaniteConstructionSystem.Extensions;

namespace NaniteConstructionSystem.Entities.Effects
{
    public class MiningHammerEffect : NaniteBlockEffectBase
    {
        private MyEntity m_hammer;
        private MyCubeBlock m_block;
        private float m_position = 0f;
        private int m_tick = 0;
        private bool m_up = true;
        private MySoundPair m_soundPair;
        private MyEntity3DSoundEmitter m_soundEmitter;
        private MyParticleEffect m_dustParticles;
        private MyParticleEffectsNameEnum m_dustEffectId;
        private MyParticleEffectsNameEnum m_dustEffectStonesId;
        private MyParticleEffectsNameEnum m_sparksEffectId;
        private DateTime m_smokeStarted;

        public MiningHammerEffect(MyCubeBlock block)
        {
            m_block = block;
            CreateMiningHammer(m_block);

            m_soundPair = new MySoundPair("NaniteHammer");
            m_soundEmitter = new MyEntity3DSoundEmitter(m_block, true);

           // m_dustEffectId = MyParticleEffectsNameEnum.Smoke_DrillDust;
           // m_dustEffectStonesId = MyParticleEffectsNameEnum.Smoke_DrillDust;
           // m_sparksEffectId = MyParticleEffectsNameEnum.Collision_Sparks;
           // m_sparksEffectId = 
            m_smokeStarted = DateTime.MinValue;                           
        }

        private void CreateMiningHammer(MyEntity block)
        {
            if (block == null)
                return;

            m_hammer = new MyEntity();
            m_hammer.Init(null, m_modelsFolder + "NUHOL_Hammer.mwm", block, null, null);
            m_hammer.Render.EnableColorMaskHsv = true;
            m_hammer.Render.ColorMaskHsv = block.Render.ColorMaskHsv;
            m_hammer.Render.PersistentFlags = MyPersistentEntityFlags2.CastShadows;
            m_hammer.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(Quaternion.Identity, new Vector3(0f, 1.5f, 0f), Vector3.One);
            m_hammer.Flags = EntityFlags.Visible | EntityFlags.NeedsDraw | EntityFlags.NeedsDrawFromParent | EntityFlags.InvalidateOnMove;
            m_hammer.OnAddedToScene(block);

            MyCubeBlockEmissive.SetEmissiveParts(m_hammer, 0.0f, Color.FromNonPremultiplied(new Vector4(0.3f, 0.15f, 0.0f, 1f)), Color.White);
        }


        public override void ActiveUpdate()
        {
            MyCubeBlockEmissive.SetEmissiveParts(m_hammer, 0.0f, Color.FromNonPremultiplied(new Vector4(0.3f, 0.15f, 0.0f, 1f)), Color.White);
            m_tick++;
            // Move upwards slowly, taking 5 seconds, waits for a second at top
            if (m_up)
            {
                if (m_tick <= 300)
                    m_position = MathExtensions.TrianglePulse(m_tick, 2f, 300) - 1f;

                if (m_tick % 360 == 0)
                {
                    m_up = false;
                    m_tick = 15;
                }
            }
            else
            {
                // Slam down, takes less than half a second, but wait for a second before going up
                if (m_tick <= 30)
                    m_position = MathExtensions.TrianglePulse(m_tick, 2f, 15) - 1f;

                if (m_tick == 30)
                {
                    // Fire off some smoke particles
                    CreateMiningSmoke(m_block);

                    // Make a thump noise
                    m_soundEmitter.PlaySound(m_soundPair);
                }

                if (m_tick % 90 == 0)
                {
                    m_smokeStarted = DateTime.Now;
                  //  CreateParticles(Vector3D.Transform(new Vector3(0f, -3f, 0f), m_block.WorldMatrix), true, true, true);

                    m_up = true;
                    m_tick = 0;
                }
            }

          /*  if (m_dustParticles != null && DateTime.Now - m_smokeStarted > TimeSpan.FromSeconds(2))
            {
                StopParticles();
            }*/

            UpdatePosition();
        }

        private void CreateMiningSmoke(MyCubeBlock block)
        {
            for(int x = 0; x < 3; x++)
            {
                for(int y = 0; y < 3; y++)
                {
                    MyParticleEffect smokeEffect;
                    if (MyParticlesManager.TryCreateParticleEffect((string)MyParticleEffectsNameEnum.Smoke_Construction, out smokeEffect))
                    {
                        smokeEffect.WorldMatrix = MatrixD.CreateTranslation(Vector3D.Transform(new Vector3(-2f + (x * 2), -3f, -2f + (y * 2)), block.WorldMatrix));
                        smokeEffect.UserScale = 1f;
                    }
                }
            }

            /*
            Vector3 halfSize = new Vector3(block.CubeGrid.GridSize) / 2;
            BoundingBox blockBox = new BoundingBox(block.Min * block.CubeGrid.GridSize - halfSize, (block.Max * block.CubeGrid.GridSize + halfSize) / 16);
            Vector3[] corners = blockBox.GetCorners();
            float particleStep = 0.25f;

            for (int e = 0; e < MyOrientedBoundingBox.StartVertices.Length; e++)
            {
                Vector3 offset = corners[MyOrientedBoundingBox.StartVertices[e]];
                float offsetLength = 0;
                float length = Vector3.Distance(offset, corners[MyOrientedBoundingBox.EndVertices[e]]);
                Vector3 offsetStep = particleStep * Vector3.Normalize(corners[MyOrientedBoundingBox.EndVertices[e]] - corners[MyOrientedBoundingBox.StartVertices[e]]);

                while (offsetLength < length)
                {
                    //Vector3D tr = Vector3D.Transform(offset + new Vector3(1.5f, -1.5f, -1.5f), block.CubeGrid.WorldMatrix);
                    Vector3D tr = Vector3D.Transform(offset, block.CubeGrid.WorldMatrix);

                    MyParticleEffect smokeEffect;
                    if (MyParticlesManager.TryCreateParticleEffect((int)MyParticleEffectsIDEnum.Smoke_Construction, out smokeEffect))
                    {
                        smokeEffect.WorldMatrix = MatrixD.CreateTranslation(tr);
                        smokeEffect.UserScale = 0.75f;
                    }

                    offsetLength += particleStep;
                    offset += offsetStep;
                }
            }
            */
        }

       /* protected void CreateParticles(Vector3D position, bool createDust, bool createSparks, bool createStones)
        {
            if (m_dustParticles != null && m_dustParticles.IsStopped)
                m_dustParticles = null;

            if (createDust)
            {
                if (m_dustParticles == null)
                {
                    //MyParticleEffectsIDEnum.Smoke_Construction
                    MyParticlesManager.TryCreateParticleEffect(createStones ? (MyParticleEffectsNameEnum)m_dustEffectStonesId : (MyParticleEffectsNameEnum)m_dustEffectId, out m_dustParticles);
                }

                if (m_dustParticles != null)
                {
                    //m_dustParticles.AutoDelete = false;
                    //m_dustParticles.Near = m_drillEntity.Render.NearFlag;
                    m_dustParticles.WorldMatrix = MatrixD.CreateTranslation(position);
                }
            }

            if (createSparks)
            {
                MyParticleEffect sparks;
                if (MyParticlesManager.TryCreateParticleEffect((string)m_sparksEffectId, out sparks))
                {
                    sparks.WorldMatrix = Matrix.CreateTranslation(position);
                    //sparks.Near = m_drillEntity.Render.NearFlag;
                }
            }
        }

        private void StopParticles()
        {
            if (m_dustParticles != null)
            {
                m_dustParticles.Stop();
                m_dustParticles = null;
            }
        }*/

        public override void InactiveUpdate()
        {
            MyCubeBlockEmissive.SetEmissiveParts(m_hammer, 1f, Color.Red, Color.White);

           /* if (m_dustParticles != null && DateTime.Now - m_smokeStarted > TimeSpan.FromSeconds(2))
            {
                StopParticles();
            }*/
        }

        public override void DeactivatingUpdate(int position, int maxPosition)
        {
            
        }

        public override void ActivatingUpdate(int position, int maxPosition)
        {

        }

        private void UpdatePosition()
        {
            m_hammer.PositionComp.LocalMatrix = Matrix.CreateFromTransformScale(Quaternion.Identity, new Vector3(0f, 1f, 0f) + new Vector3(0f, 1.4f * m_position, 0f), Vector3.One);
        }

        public override void Unload()
        {
           // StopParticles();
            m_hammer.Close();            
        }
    }
}
