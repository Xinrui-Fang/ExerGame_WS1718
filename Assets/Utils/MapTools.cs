using System.Collections.Generic;
using UnityEngine;
using MapToolsInterfaces;

public class MapTools
{
    
    // Draw a line on a grid from a to b
    public static IEnumerable<Vector2Int> BresenhamLine(Vector2Int a, Vector2Int b)
    {
        float delta_x = b.x - a.x;
        float delta_y = b.y - a.y;
        float delta_r = delta_y / delta_x;
        delta_r = delta_r > 0 ? delta_r : -delta_r;
        float error = 0;
        int y_step = delta_y > 0 ? 1 : -1;
        int y = a.y;
        for (int x = a.x; x <= b.x; x++)
        {
            yield return new Vector2Int(x, y);
            error += delta_r;
            while (error > .5f)
            {
                y += y_step;
                error--;
            }
        }
    }

    // Draw a line on a grid from a to b
    public static IEnumerable<Vector2Int> BresenhamOrthogonalLine(Vector2Int a, Vector2Int b)
    {
        float delta_x = b.x - a.x;
        float delta_y = b.y - a.y;
        
        float delta_r = delta_y / delta_x;
        delta_r = delta_r > 0 ? delta_r : -delta_r;
        float error = 0;
        int y_step = delta_y > 0 ? 1 : -1;
        int y = a.y;
        for (int x = a.x; x <= b.x; x++)
        {
            yield return new Vector2Int(x, y);
            error += delta_r;
            while (error > .5f)
            {
                y += y_step;
                if (x != b.x && y != b.y)
                    yield return new Vector2Int(x, y);
                error--;
            }
        }
    }

    //Checks wheter node is in rectangle spanned by lowerLeft and UpperRight
    public static bool InGrid(Vector2 Node, Vector2 lowerLeft, Vector2 UpperRight)
    {
        if ( Node.x < UpperRight.x && Node.x >= lowerLeft.x)
        {
            if (Node.y < UpperRight.y && Node.y >= lowerLeft.y) return true;
        }
        return false;
    }

    // Gets all coordinates adjacednt to a given nodes on a grid. gridSize is the resolution of the grid.
    // optionally use: StepSize to jump over direct adjacent nodes. width: How many neighbours of neighbours to include.
    // includeSelf: if true include node in the list of neighbours.
    public static IEnumerable<Vector2Int> GetNeighbours(Vector2Int node, Vector2Int upperLimit, Vector2Int lowerLimit, int StepSize=1, int width=1, bool includeSelf=false)
    {
        if (includeSelf)
            yield return node;
        for (int i = 1; i <= width; i++)
        {
            if (lowerLimit.x <= node.x - i * StepSize)
            {
                int xCoord = node.x - i * StepSize;
                yield return new Vector2Int(xCoord, node.y);
                for (int j = 1; j <= width; j++)
                {
                    if (lowerLimit.y <= node.y - j * StepSize)
                        yield return new Vector2Int(xCoord, node.y - j * StepSize);
                    if (upperLimit.y > node.y + j * StepSize)
                        yield return new Vector2Int(xCoord, node.y + j * StepSize);
                }
            }

            if (lowerLimit.y <= node.y - i * StepSize)
                yield return new Vector2Int(node.x, node.y - i * StepSize);
            if (upperLimit.y > node.y + i * StepSize)
                yield return new Vector2Int(node.x, node.y + i * StepSize);

            if (upperLimit.x > node.x + i * StepSize)
            {
                int xCoord = node.x + i * StepSize;
                yield return new Vector2Int(xCoord, node.y);
                for (int j = 1; j <= width; j++)
                {
                    if (lowerLimit.y <= node.y - j * StepSize)
                        yield return new Vector2Int(xCoord, node.y - j * StepSize);
                    if (upperLimit.y > node.y + j * StepSize)
                        yield return new Vector2Int(xCoord, node.y + j * StepSize);
                }
            }

        }
    }

    // Gets All Nodes in a circle of given radius around a node.
    public static IEnumerable<Vector2Int> GetCircleNodes(Vector2Int node, Vector2Int upperLimit, Vector2Int lowerLimit, int stepSize, int radius, bool includeSelf = false)
    {
        float target = radius * radius;
        foreach (Vector2Int next in GetNeighbours(node, upperLimit, lowerLimit, stepSize, radius, includeSelf))
        {
            if ((next.x - node.x) * (next.x - node.x) + (next.y - node.y) * (next.y - node.y) <= target)
                yield return next;
        }
    }

    public static void SmoothCircular(Vector2Int node, Vector2Int upperLimit, Vector2Int lowerLimit, int radius, float[,] heights, IKernel kernel)
    {
        foreach (Vector2Int circleNode in GetCircleNodes(node, upperLimit, lowerLimit, 1, radius, true))
        {
            heights[circleNode.x, circleNode.y] = kernel.ApplyKernel(circleNode, GetCircleNodes(circleNode, upperLimit, lowerLimit, 1, 1, true), heights);
        }
    }

    public static void SmoothRectangle(Vector2Int node, Vector2Int upperLimit, Vector2Int lowerLimit, int width, float[,] heights, IKernel kernel)
    {
        foreach (Vector2Int point in GetNeighbours(node, upperLimit, lowerLimit, 1, width, true))
        {
            heights[point.x, point.y] = kernel.ApplyKernel(point, GetCircleNodes(point, upperLimit, lowerLimit, 1, 1, true), heights);
        }
    }

    public static void FlattenCircular(Vector2Int node, Vector2Int upperLimit, Vector2Int lowerLimit, int radius, float[,] heights)
    {
        float avg = 0f;
        List<Vector2Int> circle = new List<Vector2Int>(GetCircleNodes(node, upperLimit, lowerLimit, 1, radius, true));
        foreach (Vector2Int point in circle)
        {
            avg += heights[point.x, point.y];
        }
        avg /= circle.Count;
        foreach (Vector2Int point in circle)
        {
            heights[point.x, point.y] = avg;
        }
    }

    public static float OctileDistance(int ax, int ay, int bx, int by)
    {
        float dx = ax - bx;
        dx = 0 < dx ? dx : -dx;
        float dy = ay - by;
        dy = 0 < dy ? dy : -dy;
        float min = dy < dx ? dy : dx;
        return (dx + dy) + (1.41f - 2f) * min;
        //return 1.41f * (dx < dy ? dx : dy) + (0 < dx - dy ? dx - dy : dy - dx);
    }

    public static Vector2Int UnfoldToPerimeter(int x, int SideLength)
    {
        int side = x / SideLength;
        int pos = x - (side * SideLength);
        switch (side)
        {
            case 0:
                return new Vector2Int(0, pos);
            case 1:
                return new Vector2Int(pos, SideLength);
            case 2:
                return new Vector2Int(SideLength, SideLength - pos);
            default:
                return new Vector2Int(SideLength - pos, 0);
        }
    }
}