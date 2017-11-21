using UnityEngine;
using System.Collections;

public class GameSettings
{
    public enum SpikyNess
    {
        Normal,
        Rigid,
        Very_rigid,
        Extreme
    }

    public enum TerrainType
    {
        Mountains,
        Valley,
        Mixed
    }

    public enum Moisture
    {
        Dry,
        Wet,
        Mixed
    }
    
    // How hight the heighest mountain can be in unity units. Values should be between 150 and 350
    public int MaxMountainHeight { get; set; }

    // How spikey the mountains should be
    public SpikyNess MountainSpikyNess { get; set; }

    // Is the world made up more of mountains or more of valleys?
    public TerrainType WorldTerrainType { get; set; }

    // Is it a dry region like a desert or a "wet" like a rainforest?
    public Moisture WorldMoisture { get; set; }

    //  The Seed for the world.
    public long WorldSeed { get; set; }

    // How detailed should the terrain be? Values should be between 2 and 8
    public int LevelOfTerrainDetail { get; set; }
}
