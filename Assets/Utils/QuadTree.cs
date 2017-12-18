using System.Collections.Generic;
using UnityEngine;

namespace Assets.Utils
{
    public enum QuadDataType
    {
        street,
        vegetation,
        building
    }

    struct QuadTreeData
    {
        public Vector2 location;
        public QuadDataType type;
        public int label;
    }

    struct RectangleBound
    {
        public readonly Vector2 Center;
        public readonly float HalfSize;

        public RectangleBound(Vector2 center, float halfSize)
        {
            Center = center;
            HalfSize = halfSize;
        }

        public bool ContainsPoint(Vector2 point)
        {
            return (point.x <= Center.x + HalfSize && point.x >= Center.x - HalfSize && point.y <= Center.y + HalfSize && point.y >= Center.y - HalfSize);
        }

        public bool InterSects(ref RectangleBound other)
        {
            return (Center.x - HalfSize < other.Center.x + other.HalfSize
                    && Center.x + HalfSize > other.Center.x - other.HalfSize
                    && Center.y - HalfSize < other.Center.y + other.HalfSize
                    && Center.y + HalfSize > other.Center.y - other.HalfSize);
        }
    }

    class QuadTree
    {
        public const int NodeCapacity = 4;
        RectangleBound Bounday;
        List<QuadTreeData> Data;

        QuadTree NW, NE, SW, SE;

        public QuadTree(RectangleBound boundary)
        {
            Bounday = boundary;
            Data = new List<QuadTreeData>(NodeCapacity);
        } 

        public bool Put(QuadTreeData data)
        {
            if (!Bounday.ContainsPoint(data.location)) return false;
            if (Data.Count < NodeCapacity)
            {
                Data.Add(data);
                return true;
            }
            if (NW == null) Subdivide();

            if (NW.Put(data)) return true;
            if (NE.Put(data)) return true;
            if (SW.Put(data)) return true;
            if (SE.Put(data)) return true;
            return false;
        }

        private void Subdivide()
        {
            float quarterSize = Bounday.HalfSize * .5f;
            NW = new QuadTree(
                new RectangleBound(
                    Bounday.Center + new Vector2(quarterSize, -quarterSize),
                    quarterSize
                    )
            );
            NE = new QuadTree(
                new RectangleBound(
                    Bounday.Center + new Vector2(quarterSize, quarterSize),
                    quarterSize
                    )
            );
            SW = new QuadTree(
                new RectangleBound(
                    Bounday.Center + new Vector2(-quarterSize, quarterSize),
                    quarterSize
                    )
            );
            SE = new QuadTree(
                new RectangleBound(
                    Bounday.Center + new Vector2(-quarterSize, -quarterSize),
                    quarterSize
                    )
            );
        }

        public bool Collides(RectangleBound scope) 
        {
            if (!Bounday.InterSects(ref scope)) return false;
            foreach (QuadTreeData dataPoint in Data)
            {
                if (scope.ContainsPoint(dataPoint.location)) return true;
            }
            if (NW != null)
            {
                if (NW.Collides(scope)) return true;
                if (NE.Collides(scope)) return true;
                if (SW.Collides(scope)) return true;
                if (SE.Collides(scope)) return true;
            }
            return false;
        }

        public bool Collides(RectangleBound scope, QuadDataType type)
        {
            if (!Bounday.InterSects(ref scope)) return false;
            foreach (QuadTreeData dataPoint in Data)
            {
                if (scope.ContainsPoint(dataPoint.location) && dataPoint.type == type) return true;
            }
            if (NW != null)
            {
                if (NW.Collides(scope, type)) return true;
                if (NE.Collides(scope, type)) return true;
                if (SW.Collides(scope, type)) return true;
                if (SE.Collides(scope, type)) return true;
            }
            return false;
        }

    }
}
