using UnityEngine;

namespace MutantArmy.Core
{
    /// <summary>
    /// Composition root da cena Game (doc 12 §3.3): mesma regra do GameBootstrap para os
    /// managers de gameplay, em ordem explícita — Level → Crowd → Gate → Boss → Combat.
    /// Roda em Awake: a cena Game carrega por LoadSceneAsync e os managers precisam estar
    /// registrados antes de qualquer Start/Update de gameplay.
    /// </summary>
    public class GameSceneBootstrap : MonoBehaviour
    {
        [Header("Managers de gameplay (IInitializable) na ORDEM canônica §3.3:\nLevel → Crowd → Gate → Boss → Combat")]
        [SerializeField] private MonoBehaviour[] _managersInOrder;

        private void Awake()
        {
            for (int i = 0; i < _managersInOrder.Length; i++)
            {
                MonoBehaviour candidate = _managersInOrder[i];
                if (candidate == null)
                {
                    Debug.LogError("[GameSceneBootstrap] Campo de manager vazio na cena Game — ver ordem canônica §3.3.", this);
                    continue;
                }
                if (candidate is IInitializable initializable) initializable.Init();
                else Debug.LogError($"[GameSceneBootstrap] {candidate.GetType().Name} não implementa IInitializable.", candidate);
            }
        }
    }
}
