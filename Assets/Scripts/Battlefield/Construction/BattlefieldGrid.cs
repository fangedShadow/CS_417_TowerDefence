using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public enum BuildSurface { Unbuildable, Land, Water }
    [Serializable]
    public sealed class GridCellDefinition
    {
        public Vector2Int Coordinates;
        public Vector3 LocalPosition;
        public BuildSurface Surface;
        public bool Buildable;
    }
    public sealed class GridCell
    {
        public GridCellDefinition Definition;
        public PlacedStructure Foundation;
        public PlacedStructure Structure;
        public bool HasFoundation => Foundation != null;
        public bool Occupied => Foundation != null || Structure != null;
    }
    public sealed class BattlefieldGrid : MonoBehaviour
    {
        [Min(1)] public float CellSize = 4;
        public Vector3 LocalOrigin;
        public GridCellDefinition[] Definitions = Array.Empty<GridCellDefinition>();
        private readonly Dictionary<Vector2Int, GridCell> cells = new Dictionary<Vector2Int, GridCell>();
        public IEnumerable<GridCell> Cells { get { Ensure(); return cells.Values; } }
        private void Ensure()
        {
            if (cells.Count != 0 || Definitions.Length == 0) return;
            foreach (var definition in Definitions) cells[definition.Coordinates] = new GridCell { Definition = definition };
        }
        public GridCell Get(Vector2Int coordinates) { Ensure(); return cells.TryGetValue(coordinates, out var cell) ? cell : null; }
        public GridCell At(Vector3 world)
        {
            var point = transform.InverseTransformPoint(world) - LocalOrigin;
            return Get(new Vector2Int(Mathf.RoundToInt(point.x / CellSize), Mathf.RoundToInt(point.z / CellSize)));
        }
        public Vector3 World(GridCell cell) => transform.TransformPoint(cell.Definition.LocalPosition);
    }
}
