using System;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Chart elemental canônico (CANON §4) como dado puro. O ElementChartSO delega
    /// para esta classe — nunca switch hardcoded (doc 12 §4.4). Célula não declarada
    /// é neutra (1.0): a matriz N×N nunca tem "buraco".
    /// </summary>
    public sealed class ElementChart
    {
        // Formato espelha o Entry do ElementChartSO (doc 12 §5.1) para o SO delegar 1:1.
        [Serializable]
        public struct Entry
        {
            public ElementType attacker;
            public ElementType defender;
            public float multiplier;
        }

        [Serializable]
        public struct BodyEntry
        {
            public ElementType attacker;
            public BodyType defender;
            public float multiplier;
        }

        private readonly float[,] _matrix;      // [atacante, defensor] elemento × elemento
        private readonly float[,] _bodyMatrix;  // [atacante, tipo de corpo]

        public ElementChart(Entry[] entries, BodyEntry[] bodyEntries)
        {
            int n = Enum.GetValues(typeof(ElementType)).Length;
            int b = Enum.GetValues(typeof(BodyType)).Length;
            _matrix = new float[n, n];
            _bodyMatrix = new float[n, b];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) _matrix[i, j] = 1f;     // neutro = 1.0
                for (int k = 0; k < b; k++) _bodyMatrix[i, k] = 1f;
            }

            if (entries != null)
                foreach (Entry e in entries)
                    _matrix[(int)e.attacker, (int)e.defender] = e.multiplier;

            if (bodyEntries != null)
                foreach (BodyEntry e in bodyEntries)
                    _bodyMatrix[(int)e.attacker, (int)e.defender] = e.multiplier;
        }

        public float GetMultiplier(ElementType atk, ElementType def)
        {
            return _matrix[(int)atk, (int)def];
        }

        public float GetBodyMultiplier(ElementType atk, BodyType def)
        {
            return _bodyMatrix[(int)atk, (int)def];
        }

        /// <summary>Entradas canônicas do CANON §4 embutidas (8 elementos; MVP usa 4).</summary>
        public static ElementChart Default()
        {
            var entries = new Entry[]
            {
                // ciclo principal: Fogo > Gelo > Raio > Fogo (+50%)
                new Entry { attacker = ElementType.Fire,      defender = ElementType.Ice,       multiplier = 1.5f },
                new Entry { attacker = ElementType.Ice,       defender = ElementType.Lightning, multiplier = 1.5f },
                new Entry { attacker = ElementType.Lightning, defender = ElementType.Fire,      multiplier = 1.5f },
                // mesmo elemento vs mesmo elemento: −50% (None fica neutro)
                new Entry { attacker = ElementType.Fire,      defender = ElementType.Fire,      multiplier = 0.5f },
                new Entry { attacker = ElementType.Ice,       defender = ElementType.Ice,       multiplier = 0.5f },
                new Entry { attacker = ElementType.Lightning, defender = ElementType.Lightning, multiplier = 0.5f },
                new Entry { attacker = ElementType.Poison,    defender = ElementType.Poison,    multiplier = 0.5f },
                new Entry { attacker = ElementType.Light,     defender = ElementType.Light,     multiplier = 0.5f },
                new Entry { attacker = ElementType.Shadow,    defender = ElementType.Shadow,    multiplier = 0.5f },
                new Entry { attacker = ElementType.Metal,     defender = ElementType.Metal,     multiplier = 0.5f },
                new Entry { attacker = ElementType.Alien,     defender = ElementType.Alien,     multiplier = 0.5f },
                // pós-MVP: Luz <-> Sombra (+50% mútuo); Metal conduz Raio (+50% recebido)
                new Entry { attacker = ElementType.Light,     defender = ElementType.Shadow,    multiplier = 1.5f },
                new Entry { attacker = ElementType.Shadow,    defender = ElementType.Light,     multiplier = 1.5f },
                new Entry { attacker = ElementType.Lightning, defender = ElementType.Metal,     multiplier = 1.5f }
            };

            var bodyEntries = new BodyEntry[]
            {
                // Veneno: +50% vs orgânicos, 0% vs máquinas e mortos-vivos
                new BodyEntry { attacker = ElementType.Poison, defender = BodyType.Organic, multiplier = 1.5f },
                new BodyEntry { attacker = ElementType.Poison, defender = BodyType.Machine, multiplier = 0f },
                new BodyEntry { attacker = ElementType.Poison, defender = BodyType.Undead,  multiplier = 0f },
                // Luz: +50% vs mortos-vivos
                new BodyEntry { attacker = ElementType.Light,  defender = BodyType.Undead,  multiplier = 1.5f }
            };

            return new ElementChart(entries, bodyEntries);
        }
    }
}
