using FM.Mathematics.DataTypes;
using Server.Core.Interfaces;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace Server.Core.WorldMap
{
    public class WorldGrid : IWorldGrid
    {
        #region Configuration
        private readonly int _cellCount;
        private readonly Vector2 _mapSize;
        private readonly Vector2 _gridMin;
        private readonly Vector2 _inverseCellSize;

        /// <summary>
        /// Stores unit IDs grouped by grid cell.
        /// </summary>
        private readonly HashSet<int>[,] _unitInCell;
        #endregion

        public WorldGrid()
        {
            _cellCount = 10;
            _mapSize = new(224f, 224f);
            Vector2 gridCenter = new(0f, 0f);
            _gridMin = gridCenter - _mapSize * 0.5f;
            _inverseCellSize = new(
                _cellCount / _mapSize.X,
                _cellCount / _mapSize.Y);
            _unitInCell = new HashSet<int>[_cellCount, _cellCount];

            for (int x = 0; x < _cellCount; x++)
            {
                for (int y = 0; y < _cellCount; y++)
                {
                    _unitInCell[x, y] = new HashSet<int>();
                }
            }
        }

        #region public APU
        public Int2 UpdateUnitCell(
            Vector2 point,
            int unitId,
            Int2 lastCell)
        {
            Int2 cell = GetCellUnit(point);

            if (cell.X == lastCell.X &&
                cell.Y == lastCell.Y)
                return lastCell;

            if (IsValidCell(lastCell))
                _unitInCell[lastCell.X, lastCell.Y].Remove(unitId);

            if (IsValidCell(cell))
            {
                _unitInCell[cell.X, cell.Y].Add(unitId);
                return cell;
            }

            return new Int2(-1, -1);
        }

        public Int2 SetUnitCell(Vector2 point, int unitId)
        {
            Int2 cell = GetCellUnit(point);

            if (IsValidCell(cell))
                _unitInCell[cell.X, cell.Y].Add(unitId);

            return cell;
        }

        public void RemoveUnit(Int2 cell, int unitId)
        {
            if (!IsValidCell(cell))
                return;

            _unitInCell[cell.X, cell.Y].Remove(unitId);
        }

        public (int[], int) GetNeighborUnitIds(Int2 cell)
        {
            int[] unitIds = ArrayPool<int>.Shared.Rent(100);
            int count = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Int2 neighbor = new(
                        cell.X + x,
                        cell.Y + y
                    );

                    if (!IsValidCell(neighbor))
                        continue;

                    foreach (int item in _unitInCell[neighbor.X, neighbor.Y])
                    {
                        if (count >= unitIds.Length)
                        {
                            int[] newArray = ArrayPool<int>.Shared.Rent(unitIds.Length * 2);

                            Array.Copy(unitIds, newArray, count);

                            ArrayPool<int>.Shared.Return(unitIds);

                            unitIds = newArray;
                        }

                        unitIds[count++] = item;
                    }
                }
            }

            return (unitIds, count);
        }
        #endregion

        #region private Handler
        private Int2 GetCellUnit(Vector2 point)
        {
            int x = (int)Math.Floor(
                (point.X - _gridMin.X) * _inverseCellSize.X);

            int y = (int)Math.Floor(
                (point.Y - _gridMin.Y) * _inverseCellSize.Y);

            return new Int2(x, y);
        }

        private bool IsValidCell(Int2 cell)
        {
            return cell.X >= 0 &&
                   cell.X < _cellCount &&
                   cell.Y >= 0 &&
                   cell.Y < _cellCount;
        }
        #endregion
    }
}
