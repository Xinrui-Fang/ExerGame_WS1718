using System.Collections.Generic;
using UnityEngine;

namespace Assets.Utils
{
    public interface IBoundary
    {
        bool ContainsPoint(Vector2 point);
        bool Intersects(ref RectangleBound other);
    }

    public enum QuadDataType
    {
        street,
        vegetation,
        building
    }

    public struct QuadTreeData
    {
        public Vector2 location;
        public QuadDataType type;
        public int label;

        public QuadTreeData(Vector2 location, QuadDataType type, int label)
        {
            this.location = location;
            this.type = type;
            this.label = label;
        }
    }

    public struct RectangleBound: IBoundary
    {
        public Vector2 Center;
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

        public bool Intersects(ref RectangleBound other)
        {
            return (Center.x - HalfSize < other.Center.x + other.HalfSize
                    && Center.x + HalfSize > other.Center.x - other.HalfSize
                    && Center.y - HalfSize < other.Center.y + other.HalfSize
                    && Center.y + HalfSize > other.Center.y - other.HalfSize);
        }

        public override string ToString()
        {
            return string.Format("Rectablge at {0} with halfSize {1}", Center, HalfSize);
        }
    }

    public struct CircleBound : IBoundary
    {
        public Vector2 Center;
        public readonly float HalfSize; // radius
        private readonly float dist;

        public CircleBound(Vector2 center, float halfSize)
        {
            Center = center;
            HalfSize = halfSize;
            dist = halfSize * halfSize;
        }

        public bool ContainsPoint(Vector2 point)
        {
            return (point - Center).magnitude < dist;
        }

        public bool Intersects(ref RectangleBound other)
        {
            // for boundary check we assume the circle is a rectangle.
            return (Center.x - HalfSize < other.Center.x + other.HalfSize
                    && Center.x + HalfSize > other.Center.x - other.HalfSize
                    && Center.y - HalfSize < other.Center.y + other.HalfSize
                    && Center.y + HalfSize > other.Center.y - other.HalfSize);
        }
    }

    public class QuadTree
    {
        public const int NodeCapacity = 4;
        public int Level = 0;
        public RectangleBound Boundary;
        List<QuadTreeData> Data;

        QuadTree NW, NE, SW, SE;

        public QuadTree(RectangleBound boundary)
        {
            Boundary = boundary;
            Data = new List<QuadTreeData>(NodeCapacity);
        } 

        public bool Put(QuadTreeData data)
        {
            if (!Boundary.ContainsPoint(data.location)) return false;
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
            float quarterSize = Boundary.HalfSize * .5f;
            NW = new QuadTree(
                new RectangleBound(
                    Boundary.Center + new Vector2(quarterSize, -quarterSize),
                    quarterSize
                    )
            );
            NE = new QuadTree(
                new RectangleBound(
                    Boundary.Center + new Vector2(quarterSize, quarterSize),
                    quarterSize
                    )
            );
            SW = new QuadTree(
                new RectangleBound(
                    Boundary.Center + new Vector2(-quarterSize, quarterSize),
                    quarterSize
                    )
            );
            SE = new QuadTree(
                new RectangleBound(
                    Boundary.Center + new Vector2(-quarterSize, -quarterSize),
                    quarterSize
                    )
            );
        }

        public bool Collides(IBoundary scope) 
        {
            if (!scope.Intersects(ref Boundary)) return false;
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

        public bool Collides(IBoundary scope, QuadDataType type)
        {
            if (!scope.Intersects(ref Boundary)) return false;
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

        public bool GetCollisions(IBoundary scope, List<QuadTreeData> Out)
        {
            bool foundSomething = false;
            if (!scope.Intersects(ref Boundary)) return false;
            foreach (QuadTreeData dataPoint in Data)
            {
                if (scope.ContainsPoint(dataPoint.location))
                {
                    Out.Add(dataPoint);
                    foundSomething = true;
                }
            }
            if (NW != null)
            {
                foundSomething = foundSomething || NW.GetCollisions(scope, Out);
                foundSomething = foundSomething || NE.GetCollisions(scope, Out);
                foundSomething = foundSomething || SW.GetCollisions(scope, Out);
                foundSomething = foundSomething || SE.GetCollisions(scope, Out);
            }
            return foundSomething;
        }

        public bool GetCollisions(IBoundary scope, QuadDataType type, List<QuadTreeData> Out)
        {
            bool foundSomething = false;
            if (!scope.Intersects(ref Boundary)) return false;
            foreach (QuadTreeData dataPoint in Data)
            {
                if (scope.ContainsPoint(dataPoint.location) && dataPoint.type == type)
                {
                    Out.Add(dataPoint);
                    foundSomething = true;
                }
            }
            if (NW != null)
            {
                foundSomething = foundSomething || NW.GetCollisions(scope, type, Out);
                foundSomething = foundSomething || NE.GetCollisions(scope, type, Out);
                foundSomething = foundSomething || SW.GetCollisions(scope, type, Out);
                foundSomething = foundSomething || SE.GetCollisions(scope, type, Out);
            }
            return foundSomething;
        }

    }
}
