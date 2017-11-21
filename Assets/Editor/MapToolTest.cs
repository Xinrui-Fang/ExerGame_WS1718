using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections.Generic;

public class MapToolTest {

	[Test]
	public void MapTools_Neighbours_Count_Test() {
        Vector2Int upper_limits = new Vector2Int(100, 100);
        Vector2Int lower_limits = new Vector2Int(0, 0);

        // All adjacent nodes on the upper corner
        IEnumerable<Vector2Int> neighbours = MapTools.GetNeighbours(upper_limits, upper_limits, lower_limits);
        Assert.AreEqual(3, new List<Vector2Int>(neighbours).Count);

        // + self
        neighbours = MapTools.GetNeighbours(upper_limits, upper_limits, lower_limits, 1, 1, true);
        Assert.AreEqual(4, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes and their adjacent nodes on the upper corner
        neighbours = MapTools.GetNeighbours(upper_limits, upper_limits, lower_limits, 1, 2);
        Assert.AreEqual(8, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes on the upper corner
        neighbours = MapTools.GetNeighbours(lower_limits, upper_limits, lower_limits);
        Assert.AreEqual(3, new List<Vector2Int>(neighbours).Count);

        // + self
        neighbours = MapTools.GetNeighbours(lower_limits, upper_limits, lower_limits, 1, 1, true);
        Assert.AreEqual(4, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes and their adjacent nodes on the upper corner
        neighbours = MapTools.GetNeighbours(lower_limits, upper_limits, lower_limits, 1, 2);
        Assert.AreEqual(8, new List<Vector2Int>(neighbours).Count);

        Vector2Int point = new Vector2Int(45, 45); 

        // All adjacent nodes to point
        neighbours = MapTools.GetNeighbours(point, upper_limits, lower_limits);
        Assert.AreEqual(8, new List<Vector2Int>(neighbours).Count);

        // + self
        neighbours = MapTools.GetNeighbours(point, upper_limits, lower_limits, 1, 1, true);
        Assert.AreEqual(9, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes and their adjacent to point
        neighbours = MapTools.GetNeighbours(point, upper_limits, lower_limits, 1, 2);
        Assert.AreEqual(24, new List<Vector2Int>(neighbours).Count);

    }

    [Test]
    public void MapTools_CircleNodes_Count_Test()
    {
        Vector2Int upper_limits = new Vector2Int(100, 100);
        Vector2Int lower_limits = new Vector2Int(0, 0);

        // All adjacent nodes on the upper corner
        IEnumerable<Vector2Int> neighbours = MapTools.GetCircleNodes(upper_limits, upper_limits, lower_limits, 1, 1);
        Assert.AreEqual(2, new List<Vector2Int>(neighbours).Count);

        // + self
        neighbours = MapTools.GetCircleNodes(upper_limits, upper_limits, lower_limits, 1, 1, true);
        Assert.AreEqual(3, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes and their adjacent nodes on the upper corner
        neighbours = MapTools.GetCircleNodes(upper_limits, upper_limits, lower_limits, 1, 2);
        Assert.AreEqual(5, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes on the upper corner
        neighbours = MapTools.GetCircleNodes(lower_limits, upper_limits, lower_limits, 1, 1);
        Assert.AreEqual(2, new List<Vector2Int>(neighbours).Count);

        // + self
        neighbours = MapTools.GetCircleNodes(lower_limits, upper_limits, lower_limits, 1, 1, true);
        Assert.AreEqual(3, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes and their adjacent nodes on the upper corner
        neighbours = MapTools.GetCircleNodes(upper_limits, upper_limits, lower_limits, 1, 2);
        Assert.AreEqual(5, new List<Vector2Int>(neighbours).Count);

        Vector2Int point = new Vector2Int(45, 45);

        // All adjacent nodes to point
        neighbours = MapTools.GetCircleNodes(point, upper_limits, lower_limits, 1, 1);
        Assert.AreEqual(4, new List<Vector2Int>(neighbours).Count);

        // + self
        neighbours = MapTools.GetCircleNodes(point, upper_limits, lower_limits, 1, 1, true);
        Assert.AreEqual(5, new List<Vector2Int>(neighbours).Count);

        // All adjacent nodes and their adjacent nodes to point
        neighbours = MapTools.GetCircleNodes(point, upper_limits, lower_limits, 1, 2);
        Assert.AreEqual(12, new List<Vector2Int>(neighbours).Count);

    }
}
