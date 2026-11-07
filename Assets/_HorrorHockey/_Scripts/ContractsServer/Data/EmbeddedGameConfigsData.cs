using System;

[Serializable]
public class EmbeddedGameConfigsData
{
    public EmbeddedGameConfigs[] EmbeddedGameConfigs { get; set; }
}

[Serializable]
public class EmbeddedGameConfigs
{
    public string Name { get; set; }
    public int Version { get; set; } = 0;
}
