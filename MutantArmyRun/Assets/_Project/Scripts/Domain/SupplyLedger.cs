using System.Collections.Generic;

namespace MutantArmy.Domain
{
    /// <summary>
    /// Plano de conversão de excedente de Supply (CANON §3.2): índices das unidades a
    /// remover (mais baratas primeiro) e as moedas creditadas. Quem EXECUTA o plano é o
    /// CrowdManager, com metering visual — o ledger só planeja.
    /// </summary>
    public struct OverflowPlan
    {
        public int[] RemoveIndices;
        public int CoinsGranted;
    }

    /// <summary>
    /// Contabilidade pura de Supply (doc 12 §4.2). Cap fixo de 60 no MVP (CANON §15);
    /// a meta eleva até 300 pós-MVP.
    /// </summary>
    public sealed class SupplyLedger
    {
        public SupplyLedger(int cap)
        {
            Cap = cap;
            Used = 0;
        }

        public int Cap { get; private set; }
        public int Used { get; private set; }

        public bool CanAdd(int cost)
        {
            return Used + cost <= Cap;
        }

        public void Add(int cost)
        {
            Used += cost;
        }

        public void Remove(int cost)
        {
            Used = System.Math.Max(0, Used - cost);
        }

        /// <summary>
        /// Calcula o plano de conversão quando o Supply estoura. Não muta <see cref="Used"/>:
        /// o CrowdManager aplica o plano unidade a unidade (metering) chamando Remove().
        /// </summary>
        /// <param name="unitsSorted">Unidades vivas ordenadas por custo ASC (mais baratas primeiro).</param>
        /// <param name="coinPerSupplyRate">Moedas por unidade convertida (chave RC supply_overflow_coin_rate).</param>
        public OverflowPlan EnforceCap(IReadOnlyList<(int index, int cost)> unitsSorted, int coinPerSupplyRate = 2)
        {
            var plan = new OverflowPlan { RemoveIndices = System.Array.Empty<int>(), CoinsGranted = 0 };
            if (Used <= Cap || unitsSorted == null || unitsSorted.Count == 0)
                return plan;

            var toRemove = new List<int>();
            int projected = Used;
            // nunca remove a última unidade: exército tem mínimo de 1 (doc 04 §7.1)
            for (int i = 0; i < unitsSorted.Count - 1 && projected > Cap; i++)
            {
                toRemove.Add(unitsSorted[i].index);
                projected -= unitsSorted[i].cost;
            }

            plan.RemoveIndices = toRemove.ToArray();
            plan.CoinsGranted = toRemove.Count * coinPerSupplyRate;
            return plan;
        }
    }
}
