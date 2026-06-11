using System;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // Doc 12 §4.2: slot filotáxico (espiral de Vogel) O(1) por índice,
    // spacing 0.45f e ângulo de ouro 2.39996 rad (137,5°).
    public class FormationMathTests
    {
        [Fact]
        public void GetSlotOffset_EDeterministico()
        {
            for (int i = 0; i < 50; i++)
            {
                Float2 a = FormationMath.GetSlotOffset(i);
                Float2 b = FormationMath.GetSlotOffset(i);
                Assert.Equal(a.x, b.x);
                Assert.Equal(a.y, b.y);
            }
        }

        [Fact]
        public void GetSlotOffset_Slot0_RaioEOSpacing()
        {
            Float2 p = FormationMath.GetSlotOffset(0);
            float r = MathF.Sqrt(p.x * p.x + p.y * p.y);
            Assert.Equal(0.45f, r, 3);
        }

        [Fact]
        public void GetSlotOffset_RaioCresceComRaizDeN_Slot99()
        {
            // r = 0.45 × √(99+1) = 4.5; tolerância ±5%
            Float2 p = FormationMath.GetSlotOffset(99);
            float r = MathF.Sqrt(p.x * p.x + p.y * p.y);
            Assert.InRange(r, 4.5f * 0.95f, 4.5f * 1.05f);
        }

        [Fact]
        public void GetSlotOffset_200Slots_DistanciaMinimaMaiorQue0Ponto3()
        {
            const int n = 200;
            var pts = new Float2[n];
            for (int i = 0; i < n; i++) pts[i] = FormationMath.GetSlotOffset(i);

            float minDist = float.MaxValue;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    float dx = pts[i].x - pts[j].x;
                    float dy = pts[i].y - pts[j].y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < minDist) minDist = d;
                }

            Assert.True(minDist > 0.3f, $"Distância mínima entre slots foi {minDist}");
        }

        [Fact]
        public void Float2_GuardaComponentesSemUnityEngine()
        {
            var v = new Float2(1.5f, -2.25f);
            Assert.Equal(1.5f, v.x);
            Assert.Equal(-2.25f, v.y);
        }
    }
}
