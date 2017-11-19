using System.Collections.Generic;
using UnityEngine;

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
    public static IEnumerable<Vector2Int> GetNeighbours(Vector2Int node, Vector2Int gridSize, int StepSize=1, int width=1, bool includeSelf=false)
    {
        if (includeSelf)
            yield return node;
        for (int i = 1; i <= width; i++)
        {
            if (0 <= node.x - i * StepSize)
            {
                int xCoord = node.x - i * StepSize;
                yield return new Vector2Int(xCoord, node.y);
                for (int j = 1; j <= width; j++)
                {
                    if (0 <= node.y - j * StepSize)
                        yield return new Vector2Int(xCoord, node.y - j * StepSize);
                    if (gridSize.y > node.y + j * StepSize)
                        yield return new Vector2Int(xCoord, node.y + j * StepSize);
                }
            }

            if (0 <= node.y - i * StepSize)
                yield return new Vector2Int(node.x, node.y - i * StepSize);
            if (gridSize.y > node.y + i * StepSize)
                yield return new Vector2Int(node.x, node.y + i * StepSize);

            if (gridSize.x > node.x + i * StepSize)
            {
                int xCoord = node.x + i * StepSize;
                yield return new Vector2Int(xCoord, node.y);
                for (int j = 1; j <= width; j++)
                {
                    if (0 <= node.y - j * StepSize)
                        yield return new Vector2Int(xCoord, node.y - j * StepSize);
                    if (gridSize.y > node.y + j * StepSize)
                        yield return new Vector2Int(xCoord, node.y + j * StepSize);
                }
            }

        }
    }

    // Gets All Nodes in a circle of given radius around a node.
    public static IEnumerable<Vector2Int> GetCircleNodes(Vector2Int node, Vector2Int gridSize, int stepSize, int radius, bool includeSelf = false)
    {
        foreach (Vector2Int neighbour in GetNeighbours(node, gridSize, radius, stepSize, includeSelf))
        {
            if (Mathf.Pow(neighbour.x - node.x, 2) + Mathf.Pow(neighbour.y - node.y, 2) <= Mathf.Pow(radius,2))
                yield return neighbour;
        }
    }
}