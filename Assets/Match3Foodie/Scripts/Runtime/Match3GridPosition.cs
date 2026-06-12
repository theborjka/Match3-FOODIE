using System;

namespace Match3Foodie
{
    [Serializable]
    public readonly struct Match3GridPosition : IEquatable<Match3GridPosition>
    {
        public Match3GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(Match3GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Match3GridPosition other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(Match3GridPosition left, Match3GridPosition right) => left.Equals(right);
        public static bool operator !=(Match3GridPosition left, Match3GridPosition right) => !left.Equals(right);
    }
}
