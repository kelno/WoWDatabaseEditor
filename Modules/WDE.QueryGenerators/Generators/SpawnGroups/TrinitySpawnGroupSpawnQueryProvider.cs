using WDE.Common.Database;
using WDE.Module.Attributes;
using WDE.QueryGenerators.Base;
using WDE.SqlQueryGenerator;

namespace WDE.QueryGenerators.Generators.SpawnGroups;

[AutoRegister]
[RequiresCore("TrinityMaster", "TrinityCata", "TrinityWrath", "AzerothCore", "Sunstrider")]
internal class TrinitySpawnGroupSpawnQueryProvider : IInsertQueryProvider<ISpawnGroupSpawn>, IDeleteQueryProvider<ISpawnGroupSpawn> 
{
    public IQuery Insert(ISpawnGroupSpawn spawn)
    {
        if (spawn.Type == SpawnGroupTemplateType.Any)
            throw new ArgumentException("Spawn Type may not be `Any`!");

        return Queries.Table(TableName)
            .Insert(new
            {
                groupId = spawn.TemplateId,
                spawnType = (int)spawn.Type,
                spawnId = spawn.Guid
            });
    }

    public IQuery Delete(ISpawnGroupSpawn spawn)
    {
        if (spawn.Type == SpawnGroupTemplateType.Any)
            throw new ArgumentException("Spawn Type may not be `Any`!");

        return Queries.Table(TableName)
            .Where(row => row.Column<uint>("groupId") == spawn.TemplateId &&
                          row.Column<uint>("spawnType") == (uint)spawn.Type &&
                          row.Column<uint>("spawnId") == spawn.Guid)
            .Delete();
    }

    public string TableName => "spawn_group";
}