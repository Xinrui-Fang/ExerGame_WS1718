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
        float delta_r = Mathf.Abs(delta_y / delta_x);
        float error = 0;
        int y = a.y;
        for (int x = a.x; x <= b.x; x++)
        {
            yield return new Vector2Int(x, y);
            error += delta_r;
            while (error > .5f)
            {
                y += (int)Mathf.Sign(delta_y);
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
        foreach (Vector2Int neighbour in GetNeighbours(node, upperLimit, lowerLimit, stepSize, radius, includeSelf))
        {
            if (Mathf.Pow(neighbour.x - node.x, 2) + Mathf.Pow(neighbour.y - node.y, 2) <= Mathf.Pow(radius,2))
                yield return neighbour;
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
        foreach (Vector2Int circleNode in GetNeighbours(node, upperLimit, lowerLimit, 1, width, true))
        {
            heights[circleNode.x, circleNode.y] = kernel.ApplyKernel(circleNode, GetCircleNodes(circleNode, upperLimit, lowerLimit, 1, 1, true), heights);
        }
    }

    public static IEnumerable<Vector2Int> EnumNotInList(IEnumerable<Vector2Int> original, IList<Vector2Int> list)
    {
        foreach (Vector2Int node in original)
        {
            if (!list.Contains(node))
            {
                yield return node;
            }
        }
    }

    public static void FlattenCircular(Vector2Int node, Vector2Int upperLimit, Vector2Int lowerLimit, int radius, float[,] heights)
    {
        float avg = 0f;
        float normalizer = 0f;
        foreach (Vector2Int point in GetCircleNodes(node, upperLimit, lowerLimit, 1, radius, true))
        {
            avg += heights[point.x, point.y];
            normalizer += 1f;
        }
        avg /= normalizer;
        foreach (Vector2Int point in GetCircleNodes(node, upperLimit, lowerLimit, 1, radius, true))
        {
            heights[point.x, point.y] = avg;
        }
    }

    public static float OctileDistance(Vector2Int a, Vector2Int b)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) + (Mathf.Sqrt(2) - 2f) * Mathf.Min(dx, dy);
    }
}