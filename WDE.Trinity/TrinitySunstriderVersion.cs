﻿using System;
using System.Collections.Generic;
using WDE.Common.CoreVersion;
using WDE.Common.Database;
using WDE.Common.Types;
using WDE.Module.Attributes;

namespace WDE.Trinity
{
    [AutoRegister]
    [SingleInstance]
    public class TrinitySunstriderVersion : ICoreVersion, IDatabaseFeatures, ISmartScriptFeatures, IConditionFeatures, IGameVersionFeatures, IEventAiFeatures
    {
        public string Tag => "Sunstrider";
        public string FriendlyName => "Sunstrider Core";
        public ImageUri Icon { get; } = new ImageUri("Icons/core_tc.png");
        public ISmartScriptFeatures SmartScriptFeatures => this;
        public IConditionFeatures ConditionFeatures => this;
        public IGameVersionFeatures GameVersionFeatures => this;
        public IDatabaseFeatures DatabaseFeatures => this;
        public IEventAiFeatures EventAiFeatures => this;
        public PhasingType PhasingType => PhasingType.PhaseMasks;
        public int Build => 12340;

        public ISet<Type> UnsupportedTables { get; } = new HashSet<Type>{typeof(IAreaTriggerTemplate), typeof(IConversationTemplate), typeof(ISceneTemplate), typeof(IAreaTriggerCreateProperties)};
        public bool AlternativeTrinityDatabase => false;
        public WaypointTables SupportedWaypoints => WaypointTables.WaypointData | WaypointTables.SmartScriptWaypoint | WaypointTables.ScriptWaypoint;
        public bool SpawnGroupTemplateHasType => false;

        public ISet<SmartScriptType> SupportedTypes { get; } = new HashSet<SmartScriptType>
        {
            SmartScriptType.Creature,
            SmartScriptType.GameObject,
            SmartScriptType.AreaTrigger,
            SmartScriptType.TimedActionList,
        };

        public string TableName => "smart_scripts";
        public string ConditionsFile => "SmartData/conditions.json";
        public string ConditionGroupsFile => "SmartData/conditions_groups.json";
        public string ConditionSourcesFile => "SmartData/condition_sources.json";
        public CharacterRaces AllRaces => CharacterRaces.AllWrath;
        public CharacterClasses AllClasses => CharacterClasses.AllWrath;
        public bool SupportsEventScripts => true;
    }
}