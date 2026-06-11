using System.Collections.Generic;
using MutantArmy.Domain;
using Xunit;

namespace Domain.Gameplay.Tests
{
    // CANON §3.2/§15: Supply cap 60 fixo no MVP; excedente vira moedas (fanfarra,
    // nunca punição); conversão remove as MAIS BARATAS primeiro; exército nunca zera.
    public class SupplyLedgerTests
    {
        [Fact]
        public void Construtor_CapCanonicoDoMvp_E60()
        {
            var ledger = new SupplyLedger(60);
            Assert.Equal(60, ledger.Cap);
            Assert.Equal(0, ledger.Used);
        }

        [Fact]
        public void Add_AcumulaUsed()
        {
            var ledger = new SupplyLedger(60);
            ledger.Add(1);   // Soldado
            ledger.Add(4);   // Mago
            ledger.Add(12);  // Gigante
            Assert.Equal(17, ledger.Used);
        }

        [Fact]
        public void Remove_LiberaUsed_SemFicarNegativo()
        {
            var ledger = new SupplyLedger(60);
            ledger.Add(4);
            ledger.Remove(4);
            Assert.Equal(0, ledger.Used);
            ledger.Remove(1);
            Assert.Equal(0, ledger.Used);
        }

        [Fact]
        public void CanAdd_RespeitaOCapExato()
        {
            var ledger = new SupplyLedger(60);
            ledger.Add(59);
            Assert.True(ledger.CanAdd(1));
            Assert.False(ledger.CanAdd(2));
        }

        [Fact]
        public void EnforceCap_SemEstouro_PlanoVazio()
        {
            var ledger = new SupplyLedger(60);
            ledger.Add(60);
            var units = new List<(int index, int cost)> { (0, 30), (1, 30) };
            OverflowPlan plan = ledger.EnforceCap(units);
            Assert.Empty(plan.RemoveIndices);
            Assert.Equal(0, plan.CoinsGranted);
        }

        [Fact]
        public void EnforceCap_Estouro_RemoveAsMaisBaratasPrimeiro()
        {
            var ledger = new SupplyLedger(10);
            ledger.Add(13);
            // lista ordenada por custo ASC (mais baratas primeiro), índice = posição no exército
            var units = new List<(int index, int cost)> { (5, 1), (2, 4), (0, 8) };
            OverflowPlan plan = ledger.EnforceCap(units);
            // remove índice 5 (custo 1) → 12, ainda estoura; remove índice 2 (custo 4) → 8 ≤ 10
            Assert.Equal(new[] { 5, 2 }, plan.RemoveIndices);
        }

        [Fact]
        public void EnforceCap_NuncaRemoveAUltimaUnidade()
        {
            var ledger = new SupplyLedger(5);
            ledger.Add(22);
            var units = new List<(int index, int cost)> { (1, 10), (0, 12) };
            OverflowPlan plan = ledger.EnforceCap(units);
            // remove a mais barata (índice 1); a última unidade (índice 0) fica mesmo acima do cap
            Assert.Equal(new[] { 1 }, plan.RemoveIndices);
        }

        [Fact]
        public void EnforceCap_UnidadeUnica_PlanoVazioMesmoEstourando()
        {
            var ledger = new SupplyLedger(5);
            ledger.Add(12);
            var units = new List<(int index, int cost)> { (0, 12) };
            OverflowPlan plan = ledger.EnforceCap(units);
            Assert.Empty(plan.RemoveIndices);
            Assert.Equal(0, plan.CoinsGranted);
        }

        [Fact]
        public void EnforceCap_MoedasPorUnidadeRemovida_TaxaPadrao2()
        {
            var ledger = new SupplyLedger(10);
            ledger.Add(13);
            var units = new List<(int index, int cost)> { (5, 1), (2, 4), (0, 8) };
            OverflowPlan plan = ledger.EnforceCap(units);
            // 2 unidades removidas × taxa default 2 (chave RC supply_overflow_coin_rate)
            Assert.Equal(4, plan.CoinsGranted);
        }

        [Fact]
        public void EnforceCap_TaxaDeMoedasSubstituivelPorRemoteConfig()
        {
            var ledger = new SupplyLedger(10);
            ledger.Add(13);
            var units = new List<(int index, int cost)> { (5, 1), (2, 4), (0, 8) };
            OverflowPlan plan = ledger.EnforceCap(units, coinPerSupplyRate: 5);
            Assert.Equal(10, plan.CoinsGranted);
        }

        [Fact]
        public void EnforceCap_NaoMutaUsed_QuemAplicaEOManagerComMetering()
        {
            // O plano é executado pelo CrowdManager com metering (~80 ms/unidade, doc 12
            // §4.2); o ledger só decrementa quando Remove() é chamado por unidade convertida.
            var ledger = new SupplyLedger(10);
            ledger.Add(13);
            var units = new List<(int index, int cost)> { (5, 1), (2, 4), (0, 8) };
            ledger.EnforceCap(units);
            Assert.Equal(13, ledger.Used);
        }
    }
}
