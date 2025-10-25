using System;
using System.Collections.Generic;
using System.Linq;

namespace Uckers.Domain.Model
{
    public readonly struct BoardPoint
    {
        public BoardPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public float DistanceSquared(BoardPoint other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            float dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }

    public sealed class BoardTopology
    {
        private const float BoardSize = 8f;
        private const int NodesPerSide = 10;
        private const float TileThickness = 0.05f;
        private const float StepHeight = 0.1f;
        private const int StepCount = 4;
        private const float BaseOffset = 1.2f;

        private static readonly IReadOnlyDictionary<PlayerId, PlayerOrientation> Orientations =
            new Dictionary<PlayerId, PlayerOrientation>
            {
                [PlayerId.Red] = PlayerOrientation.South,
                [PlayerId.Blue] = PlayerOrientation.North,
                [PlayerId.Green] = PlayerOrientation.West,
                [PlayerId.Yellow] = PlayerOrientation.East
            };

        public BoardTopology()
        {
            Lap = GenerateLap();
            EntryIndexByPlayer = BuildEntryIndices();
            HomeLaneByPlayer = BuildHomeLanes();
            BaseSpotsByPlayer = BuildBaseSpots();
            HomeEntryIndexByPlayer = BuildHomeEntryIndices();
        }

        public IReadOnlyList<BoardPoint> Lap { get; }
        public IReadOnlyDictionary<PlayerId, int> EntryIndexByPlayer { get; }
        public IReadOnlyDictionary<PlayerId, IReadOnlyList<BoardPoint>> HomeLaneByPlayer { get; }
        public IReadOnlyDictionary<PlayerId, IReadOnlyList<BoardPoint>> BaseSpotsByPlayer { get; }
        private IReadOnlyDictionary<PlayerId, int> HomeEntryIndexByPlayer { get; }

        public int TrackLength => Lap.Count;

        public IReadOnlyList<BoardPoint> GetHomeLane(PlayerId playerId)
        {
            if (!HomeLaneByPlayer.TryGetValue(playerId, out var lane))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            return lane;
        }

        public IReadOnlyList<BoardPoint> GetBaseSpots(PlayerId playerId)
        {
            if (!BaseSpotsByPlayer.TryGetValue(playerId, out var spots))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            return spots;
        }

        public int GetEntryIndex(PlayerId playerId)
        {
            if (!EntryIndexByPlayer.TryGetValue(playerId, out var index))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            return index;
        }

        public int GetHomeEntryIndex(PlayerId playerId)
        {
            if (!HomeEntryIndexByPlayer.TryGetValue(playerId, out var index))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            return index;
        }

        public int GetHomeLength(PlayerId playerId)
        {
            return GetHomeLane(playerId).Count;
        }

        public int GetFinalHomeProgress(PlayerId playerId)
        {
            return TrackLength + GetHomeLength(playerId) - 1;
        }

        public int ToHomeProgress(int homeIndex)
        {
            if (homeIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(homeIndex));
            }

            return TrackLength + homeIndex;
        }

        public int ToHomeIndex(int progress)
        {
            int index = progress - TrackLength;
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(progress));
            }

            return index;
        }

        private IReadOnlyList<BoardPoint> GenerateLap()
        {
            var nodes = new List<BoardPoint>();
            float half = BoardSize * 0.5f;
            float spacing = BoardSize / (NodesPerSide - 1);
            float y = TileThickness * 0.5f;

            for (int i = 0; i < NodesPerSide; i++)
            {
                float x = -half + spacing * i;
                nodes.Add(new BoardPoint(x, y, -half));
            }

            for (int i = 1; i < NodesPerSide; i++)
            {
                float z = -half + spacing * i;
                nodes.Add(new BoardPoint(half, y, z));
            }

            for (int i = 1; i < NodesPerSide; i++)
            {
                float x = half - spacing * i;
                nodes.Add(new BoardPoint(x, y, half));
            }

            for (int i = 1; i < NodesPerSide - 1; i++)
            {
                float z = half - spacing * i;
                nodes.Add(new BoardPoint(-half, y, z));
            }

            return nodes;
        }

        private IReadOnlyDictionary<PlayerId, int> BuildEntryIndices()
        {
            var indices = new Dictionary<PlayerId, int>();
            foreach (var kvp in Orientations)
            {
                var target = kvp.Value.EntryTarget;
                int index = FindClosestIndex(Lap, target);
                indices[kvp.Key] = index;
            }

            return indices;
        }

        private IReadOnlyDictionary<PlayerId, IReadOnlyList<BoardPoint>> BuildHomeLanes()
        {
            var lanes = new Dictionary<PlayerId, IReadOnlyList<BoardPoint>>();
            foreach (var kvp in Orientations)
            {
                lanes[kvp.Key] = GenerateHomeLane(kvp.Value);
            }

            return lanes;
        }

        private IReadOnlyDictionary<PlayerId, int> BuildHomeEntryIndices()
        {
            var indices = new Dictionary<PlayerId, int>();
            foreach (var kvp in EntryIndexByPlayer)
            {
                int homeEntry = (kvp.Value + TrackLength - 1) % TrackLength;
                indices[kvp.Key] = homeEntry;
            }

            return indices;
        }

        private IReadOnlyDictionary<PlayerId, IReadOnlyList<BoardPoint>> BuildBaseSpots()
        {
            var bases = new Dictionary<PlayerId, IReadOnlyList<BoardPoint>>();
            foreach (var kvp in Orientations)
            {
                bases[kvp.Key] = GenerateBaseSpots(kvp.Value);
            }

            return bases;
        }

        private IReadOnlyList<BoardPoint> GenerateHomeLane(PlayerOrientation orientation)
        {
            var list = new List<BoardPoint>();
            float half = BoardSize * 0.5f;
            float spacing = BoardSize / (NodesPerSide - 1);
            float yBase = TileThickness * 0.5f;

            for (int i = 0; i < StepCount; i++)
            {
                float t = (i + 1f) / StepCount;
                float y = yBase + StepHeight * (i + 1);
                float x = orientation.HomeStart.X;
                float z = orientation.HomeStart.Z;

                switch (orientation.Direction)
                {
                    case PlayerDirection.North:
                        z = Lerp(half - spacing, 0f, t);
                        break;
                    case PlayerDirection.South:
                        z = Lerp(-half + spacing, 0f, t);
                        break;
                    case PlayerDirection.East:
                        x = Lerp(half - spacing, 0f, t);
                        break;
                    case PlayerDirection.West:
                        x = Lerp(-half + spacing, 0f, t);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown direction");
                }

                list.Add(new BoardPoint(x, y, z));
            }

            return list;
        }

        private IReadOnlyList<BoardPoint> GenerateBaseSpots(PlayerOrientation orientation)
        {
            var list = new List<BoardPoint>();
            float half = BoardSize * 0.5f;
            float spacing = BoardSize / (NodesPerSide - 1);
            float y = TileThickness * 0.5f;

            BoardPoint offset;
            if (orientation.Direction == PlayerDirection.North)
            {
                offset = new BoardPoint(0f, 0f, half + BaseOffset);
            }
            else if (orientation.Direction == PlayerDirection.South)
            {
                offset = new BoardPoint(0f, 0f, -half - BaseOffset);
            }
            else if (orientation.Direction == PlayerDirection.East)
            {
                offset = new BoardPoint(half + BaseOffset, 0f, 0f);
            }
            else
            {
                offset = new BoardPoint(-half - BaseOffset, 0f, 0f);
            }

            for (int xIndex = -1; xIndex <= 1; xIndex += 2)
            {
                for (int zIndex = 0; zIndex < 2; zIndex++)
                {
                    float x = xIndex * spacing * 0.8f;
                    float z = zIndex * spacing * 0.9f;

                    if (orientation.Direction == PlayerDirection.North)
                    {
                        z = offset.Z - z;
                    }
                    else if (orientation.Direction == PlayerDirection.South)
                    {
                        z = offset.Z + z;
                    }
                    else if (orientation.Direction == PlayerDirection.East)
                    {
                        float temp = x;
                        x = offset.X - z;
                        z = temp;
                    }
                    else if (orientation.Direction == PlayerDirection.West)
                    {
                        float temp = x;
                        x = offset.X + z;
                        z = temp;
                    }

                    list.Add(new BoardPoint(x, y, z));
                }
            }

            return list;
        }

        private static int FindClosestIndex(IReadOnlyList<BoardPoint> list, BoardPoint target)
        {
            int bestIndex = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                float dist = list[i].DistanceSquared(target);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private readonly struct PlayerOrientation
        {
            private PlayerOrientation(PlayerDirection direction)
            {
                Direction = direction;
                float half = BoardSize * 0.5f;
                float spacing = BoardSize / (NodesPerSide - 1);
                float y = TileThickness * 0.5f;
                HomeStart = direction switch
                {
                    PlayerDirection.North => new BoardPoint(0f, y, half - spacing),
                    PlayerDirection.South => new BoardPoint(0f, y, -half + spacing),
                    PlayerDirection.East => new BoardPoint(half - spacing, y, 0f),
                    PlayerDirection.West => new BoardPoint(-half + spacing, y, 0f),
                    _ => throw new ArgumentOutOfRangeException(nameof(direction))
                };
                EntryTarget = direction switch
                {
                    PlayerDirection.North => new BoardPoint(0f, y, half),
                    PlayerDirection.South => new BoardPoint(0f, y, -half),
                    PlayerDirection.East => new BoardPoint(half, y, 0f),
                    PlayerDirection.West => new BoardPoint(-half, y, 0f),
                    _ => throw new ArgumentOutOfRangeException(nameof(direction))
                };
            }

            public PlayerDirection Direction { get; }
            public BoardPoint EntryTarget { get; }
            public BoardPoint HomeStart { get; }

            public static PlayerOrientation North => new PlayerOrientation(PlayerDirection.North);
            public static PlayerOrientation South => new PlayerOrientation(PlayerDirection.South);
            public static PlayerOrientation East => new PlayerOrientation(PlayerDirection.East);
            public static PlayerOrientation West => new PlayerOrientation(PlayerDirection.West);
        }

        private enum PlayerDirection
        {
            North,
            South,
            East,
            West
        }
    }
}
