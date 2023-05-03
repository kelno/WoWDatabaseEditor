using LinqToDB;
using WDE.MySqlDatabaseCommon.CommonModels;

namespace WDE.TrinityMySqlDatabase.Models;

public class SunstriderDatabase : BaseTrinityDatabase
{
    public override ITable<MySqlNpcText> NpcTexts => GetTable<MySqlGossipText>();

    public ITable<MySqlCreatureTemplateWrath> CreatureTemplate => GetTable<MySqlCreatureTemplateWrath>();
    public ITable<MySqlCreatureWrath> Creature => GetTable<MySqlCreatureWrath>();
    public ITable<MySqlBroadcastText> BroadcastTexts => GetTable<MySqlBroadcastText>();
    public ITable<TrinityString> Strings => GetTable<TrinityString>();
    public ITable<MySqlGameObjectWrath> GameObject => GetTable<MySqlGameObjectWrath>();
    public ITable<MySqlItemTemplate> ItemTemplate => GetTable<MySqlItemTemplate>();
    public ITable<MySqlCreatureModelInfo> CreatureModelInfo => GetTable<MySqlCreatureModelInfo>();
    public ITable<MySqlCreatureAddonWrath> CreatureAddon => GetTable<MySqlCreatureAddonWrath>();
    public ITable<MySqlCreatureTemplateAddon> CreatureTemplateAddon => GetTable<MySqlCreatureTemplateAddon>();
    public ITable<MySqlWrathQuestTemplateAddon> QuestTemplateAddon => GetTable<MySqlWrathQuestTemplateAddon>();
    public ITable<MySqlBroadcastTextLocale> BroadcastTextLocale => GetTable<MySqlBroadcastTextLocale>();
}