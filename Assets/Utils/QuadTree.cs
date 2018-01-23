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

    public class QuadTreeData<T>
    {
        public Vector2 location;
        public QuadDataType type;
        public T label;

        public QuadTreeData(Vector2 location, QuadDataType type, T label)
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

    public class QuadTree<T>
    {
        public const int NodeCapacity = 4;
        public int Level = 0;
        public RectangleBound Boundary;
        List<QuadTreeData<T>> Data;

        QuadTree<T> NW, NE, SW, SE;

        public QuadTree(RectangleBound boundary)
        {
            Boundary = boundary;
            Data = new List<QuadTreeData<T>>(NodeCapacity);
        } 

        public bool Put(Vector2 location, QuadDataType type, T label)
        {
                return Put(new QuadTreeData<T>(location, type, label));
        }

        public QuadTree<T>PutAndGrow(ref bool success, Vector2 location, QuadDataType type, T label)
        {
                return PutAndGrow(ref success, new QuadTreeData<T>(location, type, label));
        }
        
        // Finds new smallest root node that includes pos 
        private QuadTree<T>GrowToDataPoint(Vector2 pos)
        {
            var root = this;
            while (!root.Boundary.ContainsPoint(pos))
            {
                // iteratively grow tree.
                var old = root;
                float dx = pos.x - root.Boundary.Center.x;
                dx = dx == 0 ? 0 : Mathf.Sign(dx); // dx \in \{-1, 0 ,1}
                float dy = pos.y - root.Boundary.Center.y;
                dy = dy == 0 ? 0 : Mathf.Sign(dy); // dy \in \{-1, 0, 1}

                root = new QuadTree<T>(
                    new RectangleBound(
                        old.Boundary.Center + new Vector2(dx, dy) * old.Boundary.HalfSize, // move tree center towards pos.
                        2f * old.Boundary.HalfSize // new tree area is 4 times of the old one.
                    )
                );
                root.Subdivide(); // create subnodes.
                // override corresponding subnode with old tree.
                if (dx >= 0)
                {
                    if (dy >= 0) root.NE = old;
                    else root.SE = old;
                } else
                {
                    if (dy >= 0) root.NW = old;
                    else root.SW = old;
                }
            }
            return root;
        }

        // Puts Data in tree. returns root node. Extends tree to include new datapoint.
        public QuadTree<T>PutAndGrow(ref bool success, QuadTreeData<T> data)
        {
            // if point is outside of boundary grow tree.
            if (!Boundary.ContainsPoint(data.location))
            {
                var root = GrowToDataPoint(data.location);
                success = root.Put(data);
                return root;
            }
            // do normal put otherwise.
            if (Data.Count < NodeCapacity)
            {
                Data.Add(data);
                success = true;
                return this;
            }
            if (NW == null) Subdivide();

            if (NW.Put(data)) { success = true; return this; }
            if (NE.Put(data)) { success = true; return this; }
            if (SW.Put(data)) { success = true; return this; }
            if (SE.Put(data)) { success = true; return this; }
            success = false;
            return this;
        }

        public bool Put(QuadTreeData<T> data)
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

        // Gets Data at the exact position. Returns first match only.
        public QuadTreeData<T> Get(Vector2 position)
        {
            if (!Boundary.ContainsPoint(position)) return null;
            
            var root = this;
            while (root != null) {
                foreach (QuadTreeData<T> dataPoint in root.Data)
                {
                    if (position == dataPoint.location)
                    {
                        return dataPoint;
                    }
                }
                
                if (root.NW != null && root.NW.Boundary.ContainsPoint(position)) root = root.NW;
                else if (root.NE != null && NE.Boundary.ContainsPoint(position)) root = root.NE;
                else if (root.SW != null && root.SW.Boundary.ContainsPoint(position)) root = root.SW;
                else if (root.SE != null && root.SE.Boundary.ContainsPoint(position)) root = root.SE;
                else root = null;
            }
            return null;
        }
        
         public QuadTreeData<T> Get(Vector2Int position)
         {
            return Get(new Vector2(position.x, position.y));
         }
        
        private void Subdivide()
        {
            float quarterSize = Boundary.HalfSize * .5f;
            NW = new QuadTree<T>(
                new RectangleBound(
                    Boundary.Center + new Vector2(quarterSize, -quarterSize),
                    quarterSize
                    )
            );
            NE = new QuadTree<T>(
                new RectangleBound(
                    Boundary.Center + new Vector2(quarterSize, quarterSize),
                    quarterSize
                    )
            );
            SW = new QuadTree<T>(
                new RectangleBound(
                    Boundary.Center + new Vector2(-quarterSize, quarterSize),
                    quarterSize
                    )
            );
            SE = new QuadTree<T>(
                new RectangleBound(
                    Boundary.Center + new Vector2(-quarterSize, -quarterSize),
                    quarterSize
                    )
            );
        }

        public bool Collides(IBoundary scope) 
        {
            if (!scope.Intersects(ref Boundary)) return false;
            foreach (QuadTreeData<T> dataPoint in Data)
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
            foreach (QuadTreeData<T> dataPoint in Data)
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

        public bool GetCollisions(IBoundary scope, List<QuadTreeData<T>> Out)
        {
            bool foundSomething = false;
            if (!scope.Intersects(ref Boundary)) return false;
            foreach (QuadTreeData<T> dataPoint in Data)
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

        public bool GetCollisions(IBoundary scope, QuadDataType type, List<QuadTreeData<T>> Out)
        {
            bool foundSomething = false;
            if (!scope.Intersects(ref Boundary)) return false;
            foreach (QuadTreeData<T> dataPoint in Data)
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
