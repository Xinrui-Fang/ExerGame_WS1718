using UnityEngine;
using UnityEditor;

namespace HeightMapInterfaces
{
    /// Creates the initial HeightMap or simulates natur forces on heightmap.
    interface IHeightManipulator
    {
        float[,] ManipulateHeight(float[,] heights, int Resolution, int UnitSize);
    }

    public interface IHeightPostProcessor
    {
        // Returns processed noiseValue between -1 and 1
        float PostProcess(float noiseValue);
    }
}