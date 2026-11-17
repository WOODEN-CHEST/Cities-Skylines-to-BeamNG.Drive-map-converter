using CsToBeamNG.Converter.BeamNG.Level;
using Xunit;

namespace CsToBeamNG.Converter.Tests.BeamNG;

public sealed class LevelInfoFileTests
{
    [Fact]
    public void WritesTheKeysTheGameReads()
    {
        var metadata = new LevelMetadata
        {
            Title = "Test é level",
            Description = "A \"quoted\" description.",
            Authors = "CsToBeamNG",
            Size = (2048, 1024),
            SpawnPoints = [new LevelSpawnPoint("spawn_a", "First"), new LevelSpawnPoint("spawn_b", "Second")],
            SupportsTraffic = false,
        };

        Assert.Equal(
            """
            {
              "authors": "CsToBeamNG",
              "defaultSpawnPointName": "spawn_a",
              "description": "A \"quoted\" description.",
              "size": [
                2048,
                1024
              ],
              "spawnPoints": [
                {
                  "objectname": "spawn_a",
                  "translationId": "First"
                },
                {
                  "objectname": "spawn_b",
                  "translationId": "Second"
                }
              ],
              "supportsTimeOfDay": true,
              "supportsTraffic": false,
              "title": "Test é level"
            }

            """,
            LevelInfoFile.Create("test_level", metadata).Text());
        Assert.Equal("/levels/test_level/info.json", LevelInfoFile.Create("test_level", metadata).Path.Value);
    }

    [Fact]
    public void LeavesOutMissingOptionalKeys()
    {
        var metadata = new LevelMetadata { Title = "T", Size = (1024, 1024) };

        Assert.Equal(
            """
            {
              "size": [
                1024,
                1024
              ],
              "supportsTimeOfDay": true,
              "supportsTraffic": true,
              "title": "T"
            }

            """,
            LevelInfoFile.Create("test_level", metadata).Text());
    }
}
