using System;
using MutantArmy.Domain;
using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Chart elemental como DADO (doc 12 §5.1; valores canônicos no CANON §4) — nenhum dano
    /// do jogo tem multiplicador fora deste asset, nunca switch hardcoded (doc 12 §4.4).
    /// A resolução delega ao Domain (<see cref="ElementChart"/>, testado via dotnet test):
    /// este SO só serializa as entradas e mantém o cache construído em OnEnable.
    /// Célula não declarada é neutra (1.0) — a matriz N×N nunca tem "buraco".
    /// </summary>
    [CreateAssetMenu(menuName = "MutantArmy/ElementChart")]
    public class ElementChartSO : ScriptableObject
    {
        // Mesmo formato das entradas do Domain — o mapeamento em Build() é 1:1.
        [Serializable]
        public struct Entry
        {
            public ElementType attacker, defender;
            public float multiplier;
        }

        // Regras por tipo de corpo (CANON §4): Veneno +50% vs orgânicos, 0% vs máquinas/mortos-vivos.
        [Serializable]
        public struct BodyEntry
        {
            public ElementType attacker;
            public BodyType defender;
            public float multiplier;
        }

        [SerializeField] private Entry[] _entries;       // Fire>Ice 1.5 · Ice>Lightning 1.5 · Lightning>Fire 1.5
                                                         // mesmo elemento 0.5 · Poison vs Machine/Undead 0.0 ...
        [SerializeField] private BodyEntry[] _bodyEntries;

        [NonSerialized] private ElementChart _chart;     // cache N×N (Domain) construído em OnEnable

        public float GetMultiplier(ElementType atk, ElementType def)
        {
            if (_chart == null) Build();
            return _chart.GetMultiplier(atk, def);
        }

        public float GetBodyMultiplier(ElementType atk, BodyType def)
        {
            if (_chart == null) Build();
            return _chart.GetBodyMultiplier(atk, def);
        }

        private void Build()
        {
            int n = _entries != null ? _entries.Length : 0;
            var entries = new ElementChart.Entry[n];
            for (int i = 0; i < n; i++)
            {
                entries[i] = new ElementChart.Entry
                {
                    attacker = _entries[i].attacker,
                    defender = _entries[i].defender,
                    multiplier = _entries[i].multiplier
                };
            }

            int b = _bodyEntries != null ? _bodyEntries.Length : 0;
            var bodyEntries = new ElementChart.BodyEntry[b];
            for (int i = 0; i < b; i++)
            {
                bodyEntries[i] = new ElementChart.BodyEntry
                {
                    attacker = _bodyEntries[i].attacker,
                    defender = _bodyEntries[i].defender,
                    multiplier = _bodyEntries[i].multiplier
                };
            }

            _chart = new ElementChart(entries, bodyEntries);
        }

        private void OnEnable()
        {
            Build();
        }

        // Edição no Inspector invalida o cache — OnValidate é message de runtime do Unity,
        // não exige UnityEditor (compila no build de device).
        private void OnValidate()
        {
            _chart = null;
        }
    }
}
