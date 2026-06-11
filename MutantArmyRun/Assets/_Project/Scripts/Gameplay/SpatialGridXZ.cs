using System;
using UnityEngine;

namespace MutantArmy.Gameplay
{
    /// <summary>
    /// Grid espacial uniforme em XZ (doc 12 §6.2): reconstruída 1×/frame sem alocação,
    /// alimenta a separação local do CrowdManager e a aquisição de alvo do CombatSystem.
    /// Nunca Physics.OverlapSphere por unidade. Buckets por hashing espacial em arrays
    /// fixos (head/next) — Rebuild é O(n) e Neighbors itera só as células 3×3 vizinhas.
    /// </summary>
    public sealed class SpatialGridXZ
    {
        private readonly float _cellSize;
        private readonly int _tableMask;
        private readonly int[] _head;   // bucket → primeiro índice da lista encadeada (-1 = vazio)
        private readonly int[] _next;   // índice → próximo índice no mesmo bucket
        private readonly int[] _cellX;  // célula real de cada unidade (filtra colisão de hash)
        private readonly int[] _cellZ;
        private int _count;

        public SpatialGridXZ(float cellSize, int capacity)
        {
            if (cellSize <= 0f) cellSize = 1f;
            if (capacity < 1) capacity = 1;
            _cellSize = cellSize;

            int table = 1;
            while (table < capacity * 2) table <<= 1;
            _tableMask = table - 1;

            _head = new int[table];
            _next = new int[capacity];
            _cellX = new int[capacity];
            _cellZ = new int[capacity];
            _count = 0;
        }

        /// <summary>Reconstrói os buckets a partir das posições atuais. Zero alloc.</summary>
        public void Rebuild(Vector3[] positions, int count)
        {
            if (positions == null) { _count = 0; return; }
            _count = Math.Min(count, _next.Length);

            Array.Fill(_head, -1);
            for (int i = 0; i < _count; i++)
            {
                int cx = CellCoord(positions[i].x);
                int cz = CellCoord(positions[i].z);
                _cellX[i] = cx;
                _cellZ[i] = cz;
                int h = Hash(cx, cz);
                _next[i] = _head[h];
                _head[h] = i;
            }
        }

        /// <summary>
        /// Vizinhos da unidade i nas células 3×3 ao redor. Enumerador struct: foreach
        /// sem boxing e sem alocação.
        /// </summary>
        public NeighborEnumerator Neighbors(int i)
        {
            return new NeighborEnumerator(this, i);
        }

        private int CellCoord(float v)
        {
            return (int)MathF.Floor(v / _cellSize);
        }

        private int Hash(int cx, int cz)
        {
            // primos clássicos de hashing espacial; máscara exige tabela potência de 2
            int h = (cx * 73856093) ^ (cz * 19349663);
            return h & _tableMask;
        }

        public struct NeighborEnumerator
        {
            private readonly SpatialGridXZ _grid;
            private readonly int _self;
            private readonly int _centerX;
            private readonly int _centerZ;
            private int _cell;      // 0..8 = offsets da janela 3×3; -1 = ainda não iniciou
            private int _chain;     // posição atual na lista encadeada do bucket
            private int _targetX;   // célula-alvo do offset atual (filtra colisões de hash)
            private int _targetZ;
            private int _current;

            public NeighborEnumerator(SpatialGridXZ grid, int self)
            {
                _grid = grid;
                _self = self;
                bool valid = grid != null && self >= 0 && self < grid._count;
                _centerX = valid ? grid._cellX[self] : 0;
                _centerZ = valid ? grid._cellZ[self] : 0;
                _cell = valid ? -1 : 9;   // inválido: enumeração já termina
                _chain = -1;
                _targetX = 0;
                _targetZ = 0;
                _current = -1;
            }

            public NeighborEnumerator GetEnumerator()
            {
                return this;
            }

            public int Current => _current;

            public bool MoveNext()
            {
                while (true)
                {
                    while (_chain != -1)
                    {
                        int j = _chain;
                        _chain = _grid._next[j];
                        if (j == _self) continue;
                        // bucket pode misturar células distantes (colisão de hash): confere a célula real
                        if (_grid._cellX[j] != _targetX || _grid._cellZ[j] != _targetZ) continue;
                        _current = j;
                        return true;
                    }

                    if (_cell >= 8) return false;
                    _cell++;
                    _targetX = _centerX + (_cell % 3) - 1;
                    _targetZ = _centerZ + (_cell / 3) - 1;
                    _chain = _grid._head[_grid.Hash(_targetX, _targetZ)];
                }
            }
        }
    }
}
