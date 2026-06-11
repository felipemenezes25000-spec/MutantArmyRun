using System.Collections.Generic;
using MutantArmy.Core;
using UnityEngine;

namespace MutantArmy.Services
{
    /// <summary>
    /// Provider de analytics sem SDK (doc 12 §4.9): respeita o gate de consentimento e,
    /// em DEVELOPMENT_BUILD/editor, espelha cada evento no console — QA valida nome e
    /// parâmetros sem Firebase. Eventos disparados antes do Init ficam em fila (contrato
    /// do IAnalyticsProvider); consentimento negado descarta em vez de acumular.
    /// </summary>
    public class NullAnalyticsProvider : MonoBehaviour, IAnalyticsProvider
    {
        private readonly Queue<QueuedEvent> _preInitQueue = new Queue<QueuedEvent>();
        private bool _initialized;
        private bool _collectionEnabled;

        private struct QueuedEvent
        {
            public string name;
            public IDictionary<string, object> parameters;
        }

        public void Init(bool online, string consentStatus)
        {
            _initialized = true;
            _collectionEnabled = consentStatus != "denied";    // UMP: analytics consent-gated
            while (_preInitQueue.Count > 0)
            {
                QueuedEvent e = _preInitQueue.Dequeue();
                Log(e.name, e.parameters);
            }
        }

        public void Log(string eventName, IDictionary<string, object> parameters = null)
        {
            if (!_initialized)
            {
                _preInitQueue.Enqueue(new QueuedEvent { name = eventName, parameters = parameters });
                return;
            }
            if (!_collectionEnabled) return;

            // Sem SDK não há para onde enviar; o espelho de console existe SÓ em dev —
            // em release o provider Null é silencioso e sem custo.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var sb = new System.Text.StringBuilder("[Analytics] ").Append(eventName);
            if (parameters != null)
            {
                sb.Append(" { ");
                bool first = true;
                foreach (KeyValuePair<string, object> kv in parameters)
                {
                    if (!first) sb.Append(", ");
                    sb.Append(kv.Key).Append('=').Append(kv.Value);
                    first = false;
                }
                sb.Append(" }");
            }
            Debug.Log(sb.ToString());
#endif
        }
    }
}
