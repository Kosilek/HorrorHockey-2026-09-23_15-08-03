using FM.Mathematics.DataTypes;

namespace Server.Core.Interfaces
{
    public interface IWorldGrid
    {
        /// <summary>
        /// Updates the unit's cell in the world grid.
        /// </summary>
        /// <param name="point">
        /// The current world position of the unit.
        /// </param>
        /// <param name="unitId">
        /// The unique identifier of the unit.
        /// </param>
        /// <param name="lastCell">
        /// The previous cell occupied by the unit.
        /// Pass (-1, -1) if the unit has not been assigned to a cell.
        /// </param>
        /// <returns>
        /// The new cell coordinates.
        /// Returns (-1, -1) if the unit is outside the grid.
        /// </returns>
        Int2 UpdateUnitCell(Vector2 point, int unitId, Int2 lastCell);


        /// <summary>
        /// Assigns a unit to a cell based on its world position.
        /// </summary>
        /// <param name="point">
        /// The current world position of the unit.
        /// </param>
        /// <param name="unitId">
        /// The unique identifier of the unit.
        /// </param>
        /// <returns>
        /// The coordinates of the assigned cell.
        /// Returns (-1, -1) if the unit is outside the grid.
        /// </returns>
        Int2 SetUnitCell(Vector2 point, int unitId);


        /// <summary>
        /// Removes a unit from its assigned cell.
        /// </summary>
        /// <param name="cell">
        /// The coordinates of the cell containing the unit.
        /// </param>
        /// <param name="unitId">
        /// The unique identifier of the unit.
        /// </param>
        void RemoveUnit(Int2 cell, int unitId);

        /// <summary>
        /// Retrieves the IDs of all units located in the specified cell and its neighboring cells,
        /// including diagonal neighbors.
        /// </summary>
        /// <param name="cell">Coordinates of the central cell.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <c>unitIds</c> - an array of unit IDs rented from ArrayPool.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <c>count</c> - the actual number of unit IDs stored in the array.
        /// </description>
        /// </item>
        /// </list>
        /// The unused portion of the array may contain stale data.
        /// The returned array must be returned to ArrayPool after use.
        /// </returns>
        (int[], int) GetNeighborUnitIds(Int2 cell);
    }
}
